using System.Data;
using System.Linq.Expressions;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services.Implement;

// Error codes are part of the API contract: TasksController maps them to status codes.
public static class TaskErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string BranchRequired = "BRANCH_REQUIRED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "TASK_NOT_FOUND";
    public const string Started = "TASK_STARTED";
    public const string Completed = "TASK_COMPLETED";
}

public sealed class TaskService(
    ApplicationDbContext db,
    IMatrixPeopleResolver peopleResolver,
    IEmailNotificationService emailNotifications,
    IMatrixRoleCatalog roleCatalog,
    IConfiguration configuration,
    TimeProvider time) : ITaskService
{
    private const string TeacherLabel = "Giáo viên";

    // Cùng khoá và mặc định với policy TaskRead trong Program.cs.
    public static string[] TeacherRoleCodes(IConfiguration configuration)
    {
        var configured = configuration.GetSection("TaskAuth:TeacherRoleCodes").GetChildren()
            .Select(item => item.Value).OfType<string>().ToArray();
        return configured.Length > 0 ? configured : ["GIAO_VIEN", "TEACHER"];
    }

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public async Task<ServiceResult<DirectoryPage<TaskListItem>>> ListAsync(
        TaskActor actor,
        TaskListQuery query,
        CancellationToken cancellationToken)
    {
        var errors = TaskRules.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<TaskListItem>>(errors);
        if (!actor.IsPrincipal && await BranchOfAsync(actor.UserId, cancellationToken) is null)
            return BranchRequired<DirectoryPage<TaskListItem>>();

        var tasks = Involving(actor.UserId);
        if (!string.IsNullOrEmpty(query.TaskType)) tasks = tasks.Where(task => task.TaskType == query.TaskType);
        if (query.Status == TaskRules.Overdue) tasks = tasks.Where(OverdueBefore(Today.ToDateTime(TimeOnly.MinValue)));
        else if (!string.IsNullOrEmpty(query.Status)) tasks = tasks.Where(task => task.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = $"%{query.Keyword.Trim()}%";
            tasks = tasks.Where(task =>
                EF.Functions.Like(task.Name, pattern) ||
                (task.Description != null && EF.Functions.Like(task.Description, pattern)));
        }

        var totalCount = await tasks.CountAsync(cancellationToken);
        var rows = await tasks
            .OrderByDescending(task => task.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var ids = rows.Select(task => task.Id).ToArray();
        var notStarted = await db.WorkTasks.Where(task => ids.Contains(task.Id)).WhereNotStarted(db)
            .Select(task => task.Id).ToListAsync(cancellationToken);
        var people = await peopleResolver.ResolveAsync(
            rows.SelectMany(task => new ulong?[] { task.AssignedToUserId, task.CreatedByUserId }), cancellationToken);
        var today = Today;

        var items = rows.Select(task => new TaskListItem(
            task.Id,
            task.TaskType,
            task.Name,
            task.Status,
            TaskRules.IsOverdue(task.Status, task.DueAt, today),
            task.DueAt,
            Person(people, task.AssignedToUserId, task),
            Person(people, task.CreatedByUserId, task),
            CanEdit(actor, task),
            CanDelete(actor, task) && notStarted.Contains(task.Id))).ToArray();
        return ServiceResult<DirectoryPage<TaskListItem>>.Success(
            new DirectoryPage<TaskListItem>(items, query.Page, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<TaskDetail>> GetAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken)
    {
        if (!actor.IsPrincipal && await BranchOfAsync(actor.UserId, cancellationToken) is null)
            return BranchRequired<TaskDetail>();

        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null) return NotFound<TaskDetail>();
        if (task.CreatedByUserId != actor.UserId && task.AssignedToUserId != actor.UserId)
            return Forbidden<TaskDetail>("Bạn không phụ trách nhiệm vụ này nên không xem được.");

        return ServiceResult<TaskDetail>.Success(await DetailAsync(actor, task, cancellationToken));
    }

    public async Task<ServiceResult<TaskOptions>> GetOptionsAsync(TaskActor actor, CancellationToken cancellationToken)
    {
        if (await BranchOfAsync(actor.UserId, cancellationToken) is not { } branchId) return BranchRequired<TaskOptions>();

        // Tổ trưởng giao việc cho giáo viên; PHT giao cho Tổ trưởng, từ ma trận đã duyệt của phân hiệu.
        var teachers = actor.IsTeamLead ? await PersonOptionsAsync(branchId, TeacherRoleCodes(configuration), cancellationToken) : [];
        var teamLeads = actor.IsPht ? await PersonOptionsAsync(branchId, roleCatalog.TeamLeadRoleCodes, cancellationToken) : [];
        var matrices = actor.IsPht
            ? await db.ExamMatrices.AsNoTracking()
                .Where(matrix => matrix.Status == MatrixStatusCodes.Approved && matrix.BranchId == branchId)
                .OrderBy(matrix => matrix.Name)
                .Select(matrix => new TaskMatrixOption(matrix.Id, matrix.Name, matrix.Subject.Name,
                    matrix.GradeLevel.Name, ""))
                .ToListAsync(cancellationToken)
            : [];
        return ServiceResult<TaskOptions>.Success(new TaskOptions(teachers, teamLeads, matrices));
    }

    public Task<ServiceResult<TaskDetail>> UpdateMatrixAsync(
        TaskActor actor,
        ulong taskId,
        CreateMatrixTaskRequest request,
        CancellationToken cancellationToken) =>
        UpdateAsync(actor, taskId, TaskTypeCodes.Matrix, async (task, branchId) =>
        {
            // The scope lives on the task's matrix, created with the task.
            var matrix = await db.ExamMatrices.SingleOrDefaultAsync(item => item.TaskId == task.Id, cancellationToken);
            var errors = TaskRules.MatrixTask(request, task, Today);
            var scope = await CheckMatrixAsync(branchId, request, task, matrix?.Scope, errors, cancellationToken);
            if (errors.Count > 0 || scope is null) return Invalid<TaskDetail>(errors);

            var locked = task.AssignedToUserId != request.AssignedToUserId || matrix?.Scope != scope;
            if (locked && await IsStartedAsync(task.Id, cancellationToken)) return StartedFailure();

            var notify = task.AssignedToUserId != request.AssignedToUserId || task.DueAt != request.DueAt;
            task.Name = request.Name.Trim();
            task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            task.DueAt = request.DueAt;
            task.AssignedToUserId = request.AssignedToUserId;
            if (matrix is not null && locked)
            {
                matrix.BranchId = scope.Value.BranchId;
                matrix.SubjectId = scope.Value.SubjectId;
                matrix.GradeLevelId = scope.Value.GradeLevelId;
                matrix.CreatedByUserId = request.AssignedToUserId;
            }

            if (notify) await QueueMatrixReassignmentAsync(task, matrix?.BranchId ?? branchId, actor.UserId, cancellationToken);
            return null;
        }, cancellationToken);

    public async Task<ServiceResult<TaskDetail>> CreateExamAsync(
        TaskActor actor,
        SaveExamTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!actor.IsPht) return Forbidden<TaskDetail>("Chỉ Phó Hiệu trưởng được giao nhiệm vụ tạo đề thi.");
        if (await BranchOfAsync(actor.UserId, cancellationToken) is not { } branchId) return BranchRequired<TaskDetail>();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var errors = TaskRules.ExamTask(request, null, Today);
        await CheckExamAsync(branchId, request, null, null, errors, cancellationToken);
        if (errors.Count > 0) return Invalid<TaskDetail>(errors);

        var task = new WorkTask
        {
            TaskType = TaskTypeCodes.Exam,
            Status = MatrixTaskStatusCodes.Assigned,
            Name = request.Name!.Trim(),
            CreatedByUserId = actor.UserId,
            AssignedToUserId = request.AssignedToUserId,
            DueAt = request.DueAt,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkTasks.Add(task);
        AddPapers(task, request);
        await db.SaveChangesAsync(cancellationToken);
        var detail = await DetailAsync(actor, task, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<TaskDetail>.Success(detail);
    }

    public Task<ServiceResult<TaskDetail>> UpdateExamAsync(
        TaskActor actor,
        ulong taskId,
        SaveExamTaskRequest request,
        CancellationToken cancellationToken) =>
        UpdateAsync(actor, taskId, TaskTypeCodes.Exam, async (task, branchId) =>
        {
            // Mỗi đề cần tạo là một exam_set Nháp của nhiệm vụ; ma trận nguồn nằm trên các đề đó.
            var current = await db.ExamSets.Where(set => set.TaskId == task.Id).ToListAsync(cancellationToken);
            var matrixId = current.Select(set => set.ExamMatrixId).FirstOrDefault();
            var errors = TaskRules.ExamTask(request, task, Today);
            await CheckExamAsync(branchId, request, task, matrixId, errors, cancellationToken);
            if (errors.Count > 0) return Invalid<TaskDetail>(errors);

            var papersChanged = current.Count != request.PaperCount;
            var locked = papersChanged ||
                task.AssignedToUserId != request.AssignedToUserId ||
                matrixId != request.ExamMatrixId;
            if (locked && await IsStartedAsync(task.Id, cancellationToken)) return StartedFailure();

            task.Name = request.Name!.Trim();
            task.DueAt = request.DueAt;
            task.AssignedToUserId = request.AssignedToUserId;
            if (papersChanged)
            {
                // Chưa bắt đầu nên chưa đề nào được soạn: dựng lại Đề 1 … Đề N.
                db.ExamSets.RemoveRange(current);
                AddPapers(task, request);
            }
            else if (locked)
            {
                foreach (var set in current) set.ExamMatrixId = request.ExamMatrixId;
            }

            return null;
        }, cancellationToken);

    public async Task<ServiceResult<TaskDetail>> CreateQuestionAsync(
        TaskActor actor,
        SaveQuestionTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!actor.IsTeamLead) return Forbidden<TaskDetail>("Chỉ Tổ trưởng được giao nhiệm vụ biên soạn câu hỏi.");
        if (await BranchOfAsync(actor.UserId, cancellationToken) is not { } branchId) return BranchRequired<TaskDetail>();

        var errors = TaskRules.QuestionTask(request, null, Today);
        await CheckQuestionAsync(branchId, request, null, [], errors, cancellationToken);
        if (errors.Count > 0) return Invalid<TaskDetail>(errors);

        var task = new WorkTask
        {
            TaskType = TaskTypeCodes.Question,
            Status = MatrixTaskStatusCodes.Assigned,
            Name = request.Name!.Trim(),
            CreatedByUserId = actor.UserId,
            AssignedToUserId = request.AssignedToUserId,
            DueAt = request.DueAt,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkTasks.Add(task);
        AddLessons(task, request.Lessons!);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<TaskDetail>.Success(await DetailAsync(actor, task, cancellationToken));
    }

    public Task<ServiceResult<TaskDetail>> UpdateQuestionAsync(
        TaskActor actor,
        ulong taskId,
        SaveQuestionTaskRequest request,
        CancellationToken cancellationToken) =>
        UpdateAsync(actor, taskId, TaskTypeCodes.Question, async (task, branchId) =>
        {
            var current = await db.QuestionTasks.Include(lesson => lesson.Details)
                .Where(lesson => lesson.TaskId == task.Id).OrderBy(lesson => lesson.Id).ToListAsync(cancellationToken);
            var errors = TaskRules.QuestionTask(request, task, Today);
            await CheckQuestionAsync(branchId, request, task, current.Select(lesson => lesson.LessonId).ToArray(), errors, cancellationToken);
            if (errors.Count > 0) return Invalid<TaskDetail>(errors);
            var lessonsChanged = !current.Select(lesson => (lesson.LessonId,
                    Count(lesson, MatrixCognitiveLevels.Knowledge),
                    Count(lesson, MatrixCognitiveLevels.Understanding),
                    Count(lesson, MatrixCognitiveLevels.Application)))
                .SequenceEqual(request.Lessons!.Select(lesson => (lesson.LessonId, lesson.Knowledge, lesson.Understanding, lesson.Application)));
            // shortcut: sau khi giáo viên bắt đầu thì khoá cả số câu; cho tăng số câu (không dưới số đã soạn, GB-32) khi có chức năng soạn câu hỏi.
            if ((lessonsChanged || task.AssignedToUserId != request.AssignedToUserId) && await IsStartedAsync(task.Id, cancellationToken))
                return StartedFailure();

            task.Name = request.Name!.Trim();
            task.AssignedToUserId = request.AssignedToUserId;
            task.DueAt = request.DueAt;
            if (lessonsChanged)
            {
                db.QuestionTasks.RemoveRange(current);
                // Xoá trước rồi mới thêm: bài giữ lại sẽ trùng uq_question_task_task_id_lesson_id nếu chung một lần lưu.
                await db.SaveChangesAsync(cancellationToken);
                AddLessons(task, request.Lessons!);
            }

            return null;
        }, cancellationToken);

    public async Task<ServiceResult<string>> DeleteAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken)
    {
        if (await BranchOfAsync(actor.UserId, cancellationToken) is null) return BranchRequired<string>();

        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null) return NotFound<string>();
        if (task.CreatedByUserId != actor.UserId) return Forbidden<string>("Chỉ người giao nhiệm vụ mới được xóa.");

        // Khoá nhiệm vụ, kiểm tra "chưa bắt đầu" rồi xoá: người thực hiện vừa tạo sản phẩm thì không xoá gì.
        // Bài học, số câu (CASCADE), ma trận và các đề Nháp rỗng của nhiệm vụ bị xoá theo.
        if (await db.DeleteTaskIfNotStartedAsync(taskId, cancellationToken)) return ServiceResult<string>.Success(task.Name);

        return await db.WorkTasks.AnyAsync(item => item.Id == taskId, cancellationToken)
            ? ServiceResult<string>.Failure(TaskErrorCodes.Started, "Nhiệm vụ đã được thực hiện, không thể xóa.")
            : NotFound<string>();
    }

    // Sửa trong một transaction, khoá dòng nhiệm vụ: người thực hiện tạo ma trận, đề hay câu hỏi (có FK tới
    // nhiệm vụ) phải chờ lượt sửa này xong, nên kiểm tra "chưa bắt đầu" không bị lệch (GB-56).
    private async Task<ServiceResult<TaskDetail>> UpdateAsync(
        TaskActor actor,
        ulong taskId,
        string taskType,
        Func<WorkTask, ulong, Task<ServiceResult<TaskDetail>?>> apply,
        CancellationToken cancellationToken)
    {
        if (await BranchOfAsync(actor.UserId, cancellationToken) is not { } branchId) return BranchRequired<TaskDetail>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockTaskAsync(taskId, cancellationToken);
        var task = await db.WorkTasks.SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null || task.TaskType != taskType) return NotFound<TaskDetail>();
        if (task.CreatedByUserId != actor.UserId) return Forbidden<TaskDetail>("Chỉ người giao nhiệm vụ mới được chỉnh sửa.");
        if (task.Status == MatrixTaskStatusCodes.Completed)
            return ServiceResult<TaskDetail>.Failure(TaskErrorCodes.Completed, "Nhiệm vụ đã hoàn thành, không thể chỉnh sửa.");

        if (await apply(task, branchId) is { } failure) return failure;

        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var detail = await DetailAsync(actor, task, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<TaskDetail>.Success(detail);
    }

    private async Task<TaskDetail> DetailAsync(TaskActor actor, WorkTask task, CancellationToken cancellationToken)
    {
        var people = await peopleResolver.ResolveAsync(
            [task.CreatedByUserId, task.AssignedToUserId], cancellationToken);
        var started = await IsStartedAsync(task.Id, cancellationToken);
        var scope = await ScopeAsync(task.Id, cancellationToken);

        return new TaskDetail(
            task.Id,
            task.TaskType,
            task.Name,
            task.Description,
            task.Status,
            TaskRules.IsOverdue(task.Status, task.DueAt, Today),
            started,
            CanEdit(actor, task),
            CanDelete(actor, task) && !started,
            people.GetValueOrDefault(task.CreatedByUserId),
            // Lưu theo giờ UTC: đánh dấu để client đổi đúng sang giờ địa phương.
            DateTime.SpecifyKind(task.CreatedAt, DateTimeKind.Utc),
            Person(people, task.AssignedToUserId, task),
            Person(people, task.CreatedByUserId, task),
            task.DueAt,
            scope,
            task.TaskType == TaskTypeCodes.Matrix ? await MatrixOfAsync(actor, task, cancellationToken) : null,
            task.TaskType == TaskTypeCodes.Exam ? await ExamOfAsync(task, cancellationToken) : null,
            task.TaskType == TaskTypeCodes.Question ? await LessonsOfAsync(task.Id, cancellationToken) : []);
    }

    // Phạm vi của nhiệm vụ ma trận là ma trận của nó; của nhiệm vụ đề là ma trận nguồn của các đề. Không còn năm học, học kỳ.
    private async Task<TaskScope?> ScopeAsync(ulong taskId, CancellationToken cancellationToken)
    {
        var matrix = await db.ExamMatrices.AsNoTracking()
            .Where(item => item.TaskId == taskId || db.ExamSets.Any(set => set.TaskId == taskId && set.ExamMatrixId == item.Id))
            .Select(item => new { item.BranchId, item.SubjectId, item.GradeLevelId, Subject = item.Subject.Name, Grade = item.GradeLevel.Name })
            .FirstOrDefaultAsync(cancellationToken);
        return matrix is null
            ? null
            : new TaskScope(new MatrixScope(matrix.BranchId, matrix.SubjectId, matrix.GradeLevelId).Key, matrix.Subject,
                matrix.Grade, "", null, null, null, matrix.SubjectId, matrix.GradeLevelId);
    }

    // GB-47: ma trận nháp chỉ Tổ trưởng được giao thấy; người khác chỉ biết là đang soạn.
    private async Task<TaskMatrixItem?> MatrixOfAsync(TaskActor actor, WorkTask task, CancellationToken cancellationToken)
    {
        var matrix = await db.ExamMatrices.AsNoTracking()
            .Where(item => item.TaskId == task.Id)
            .Select(item => new { item.Id, item.Name, item.Status, HasDetails = item.Details.Any() })
            .SingleOrDefaultAsync(cancellationToken);
        // Ma trận tạo sẵn cùng nhiệm vụ: chưa có gì thì người xem vẫn thấy "chưa bắt đầu".
        if (matrix is null ||
            (task.Status == MatrixTaskStatusCodes.Assigned && matrix.Status == MatrixStatusCodes.Draft && !matrix.HasDetails))
            return null;

        // Bị trả về: lần từ chối gần nhất nằm ở task_history (nội dung rỗng = không có nhận xét).
        var rejection = matrix.Status == MatrixStatusCodes.Draft
            ? await db.TaskHistories.AsNoTracking()
                .Where(history => history.TaskId == task.Id)
                .OrderByDescending(history => history.Id)
                .Select(history => new { history.Content, history.CreateAt })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        // Ma trận không lưu ngày sửa: mỗi lần lưu đầu/nộp/từ chối/duyệt đều cập nhật trạng thái nhiệm vụ.
        var updatedAt = task.UpdatedAt ?? task.CreatedAt;
        var hidden = matrix.Status == MatrixStatusCodes.Draft && task.AssignedToUserId != actor.UserId;
        return new TaskMatrixItem(
            hidden ? null : matrix.Id,
            hidden ? null : matrix.Name,
            matrix.Status,
            DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc),
            string.IsNullOrEmpty(rejection?.Content) ? null : rejection.Content,
            rejection is null ? null : DateTime.SpecifyKind(rejection.CreateAt, DateTimeKind.Utc));
    }

    private async Task<TaskExamInfo?> ExamOfAsync(WorkTask task, CancellationToken cancellationToken)
    {
        var papers = await db.ExamSets.AsNoTracking()
            .Where(set => set.TaskId == task.Id)
            .OrderBy(set => set.Id)
            .Select(set => new { set.Name, set.ExamMatrixId })
            .ToListAsync(cancellationToken);
        if (papers.FirstOrDefault()?.ExamMatrixId is not { } matrixId) return null;

        var matrix = await db.ExamMatrices.AsNoTracking()
            .Where(item => item.Id == matrixId)
            .Select(item => new
            {
                item.Name,
                Subject = item.Subject.Name,
                Grade = item.GradeLevel.Name
            })
            .SingleAsync(cancellationToken);
        return new TaskExamInfo(matrixId, matrix.Name, matrix.Subject, matrix.Grade, "",
            papers.Select(paper => new TaskPaperItem(paper.Name)).ToList());
    }

    private Task<List<TaskLessonItem>> LessonsOfAsync(ulong taskId, CancellationToken cancellationToken) =>
        db.QuestionTasks.AsNoTracking()
            .Where(lesson => lesson.TaskId == taskId)
            .OrderBy(lesson => lesson.Id)
            .Select(lesson => new TaskLessonItem(
                lesson.LessonId,
                lesson.Lesson.ChapterId,
                lesson.Lesson.Chapter.GradeLevelId,
                lesson.Lesson.Chapter.GradeLevel.Name,
                "",
                lesson.Lesson.Chapter.Title,
                "",
                lesson.Lesson.Title,
                lesson.Details.Where(detail => detail.CognitiveLevel == MatrixCognitiveLevels.Knowledge)
                    .Select(detail => (uint?)detail.QuestionCount).FirstOrDefault() ?? 0,
                lesson.Details.Where(detail => detail.CognitiveLevel == MatrixCognitiveLevels.Understanding)
                    .Select(detail => (uint?)detail.QuestionCount).FirstOrDefault() ?? 0,
                lesson.Details.Where(detail => detail.CognitiveLevel == MatrixCognitiveLevels.Application)
                    .Select(detail => (uint?)detail.QuestionCount).FirstOrDefault() ?? 0,
                lesson.Details.SelectMany(detail => detail.Questions).Count(),
                lesson.Details.SelectMany(detail => detail.Questions)
                    .Count(question => question.Status == "SUBMITTED")))
            .ToListAsync(cancellationToken);

    // Chỉ kiểm tra lại phần đổi: người nhận đã nghỉ thì vẫn sửa được tên, hạn, yêu cầu. Trả phạm vi
    // (academicContextId là MatrixScope.Key), null khi không hợp lệ.
    private async Task<MatrixScope?> CheckMatrixAsync(
        ulong branchId,
        CreateMatrixTaskRequest request,
        WorkTask current,
        MatrixScope? currentScope,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (request.AssignedToUserId != 0 && request.AssignedToUserId != current.AssignedToUserId &&
            !await PeopleWithRoles(branchId, roleCatalog.TeamLeadRoleCodes).AnyAsync(user => user.Id == request.AssignedToUserId, cancellationToken))
            errors.TryAdd("assignedToUserId", ["Người nhận phải là Tổ trưởng đang hoạt động thuộc cùng phân hiệu."]);
        if (request.AcademicContextId == 0) return null;
        if (!MatrixScope.TryParse(request.AcademicContextId, out var scope))
        {
            errors.TryAdd("academicContextId", ["Phạm vi ma trận không thuộc phân hiệu của bạn."]);
            return null;
        }

        // A scope exists through its curriculum chapters (same rule as the matrix reference data).
        if (scope != currentScope &&
            (scope.BranchId != branchId ||
             !await db.Chapters.AnyAsync(chapter => chapter.SchoolBranchId == scope.BranchId &&
                 chapter.SubjectId == scope.SubjectId && chapter.GradeLevelId == scope.GradeLevelId, cancellationToken)))
        {
            errors.TryAdd("academicContextId", ["Phạm vi ma trận không thuộc phân hiệu của bạn."]);
            return null;
        }

        return scope;
    }

    // GB-57: ma trận nguồn đã duyệt và cùng phân hiệu với người giao.
    // Khi sửa, ma trận giữ nguyên thì không kiểm tra lại (ma trận đã lưu trữ vẫn sửa được hạn).
    private async Task CheckExamAsync(
        ulong branchId,
        SaveExamTaskRequest request,
        WorkTask? current,
        ulong? currentMatrixId,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (request.AssignedToUserId != 0 && request.AssignedToUserId != current?.AssignedToUserId &&
            !await PeopleWithRoles(branchId, roleCatalog.TeamLeadRoleCodes).AnyAsync(user => user.Id == request.AssignedToUserId, cancellationToken))
            errors.TryAdd("assignedToUserId", ["Người thực hiện phải là Tổ trưởng đang hoạt động thuộc cùng phân hiệu."]);
        if (current is not null && currentMatrixId == request.ExamMatrixId) return;

        // Locking read sees the current status even after another query started a repeatable-read snapshot.
        var matrix = request.ExamMatrixId == 0 ? null : (await db.ExamMatrices
            .FromSqlInterpolated($"SELECT * FROM matrix WHERE matrix_id = {request.ExamMatrixId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();
        if (matrix is not null && (matrix.Status != MatrixStatusCodes.Approved || matrix.BranchId != branchId))
            matrix = null;
        if (request.ExamMatrixId != 0 && matrix is null)
            errors.TryAdd("examMatrixId", ["Ma trận phải đã được duyệt và thuộc phân hiệu của bạn."]);
    }

    // Khi sửa, chỉ kiểm tra người và bài học mới đổi: giáo viên đã nghỉ vẫn để nguyên được để sửa hạn.
    private async Task CheckQuestionAsync(
        ulong branchId,
        SaveQuestionTaskRequest request,
        WorkTask? current,
        IReadOnlyCollection<ulong> currentLessonIds,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var teachers = PeopleWithRoles(branchId, TeacherRoleCodes(configuration));
        if (request.AssignedToUserId != 0 && request.AssignedToUserId != current?.AssignedToUserId &&
            !await teachers.AnyAsync(user => user.Id == request.AssignedToUserId, cancellationToken))
            errors.TryAdd("assignedToUserId", ["Người thực hiện phải là giáo viên đang hoạt động của phân hiệu."]);

        if (errors.ContainsKey("lessons")) return;
        var added = request.Lessons!.Select(lesson => lesson.LessonId).Where(id => !currentLessonIds.Contains(id)).ToArray();
        if (added.Length > 0 &&
            await db.Lessons.CountAsync(lesson => added.Contains(lesson.Id) && lesson.Chapter.SchoolBranchId == branchId, cancellationToken)
            != added.Length)
            errors.TryAdd("lessons", ["Bài học đã chọn không còn trong chương trình của phân hiệu."]);
    }

    // Một exam_set Nháp rỗng cho mỗi đề cần tạo; thứ tự đề = thứ tự id.
    private const uint DefaultPaperMinutes = 40;

    private void AddPapers(WorkTask task, SaveExamTaskRequest request)
    {
        foreach (var name in TaskRules.PaperNames(request.PaperCount))
            db.ExamSets.Add(new ExamSet
            {
                Task = task,
                Name = name,
                Status = ExamSetStatusCodes.Draft,
                ExamMatrixId = request.ExamMatrixId,
                // shortcut: the exam task request has no duration yet; the exam owner sets the real one.
                DurationMinutes = DefaultPaperMinutes
            });
    }

    private void AddLessons(WorkTask task, IEnumerable<QuestionTaskLessonRequest> lessons)
    {
        foreach (var lesson in lessons)
        {
            var questionTask = new QuestionTask
            {
                Task = task,
                LessonId = lesson.LessonId,
                Status = MatrixTaskStatusCodes.Assigned,
                AssignedQuestionCount = lesson.Knowledge + lesson.Understanding + lesson.Application
            };
            // Mức không có câu nào thì không lưu dòng (ck_question_task_details_count > 0).
            foreach (var (level, count) in TaskRules.Levels(lesson).Where(level => level.Count > 0))
                questionTask.Details.Add(new QuestionTaskDetail
                {
                    CognitiveLevel = level,
                    QuestionCount = count,
                    QuestionType = MatrixQuestionTypes.MultipleChoice
                });
            db.QuestionTasks.Add(questionTask);
        }
    }

    // GB-58: báo lại nhiệm vụ cho Tổ trưởng khi đổi người nhận hoặc hạn, cùng mẫu email "Giao nhiệm vụ lập ma trận".
    // branchId: the branch of the task's matrix.
    private async Task QueueMatrixReassignmentAsync(WorkTask task, ulong branchId, ulong actorId, CancellationToken cancellationToken)
    {
        var place = await db.SchoolBranches.AsNoTracking()
            .Where(branch => branch.Id == branchId)
            .Select(branch => new { branch.SchoolId, SchoolBranchId = branch.Id, School = branch.School.Name, Branch = branch.Name })
            .SingleAsync(cancellationToken);
        var origin = configuration["NotificationEmail:FrontendOrigin"];
        var path = $"/matrix-tasks/{task.Id}";
        await emailNotifications.QueueEventAsync(place.SchoolId, place.SchoolBranchId, "MATRIX_ASSIGNED",
            $"matrix-task:{task.Id}:updated:{db.Database.CurrentTransaction!.TransactionId:N}", actorId,
            new Dictionary<string, string>
            {
                ["schoolName"] = place.School,
                ["branchName"] = place.Branch,
                ["taskName"] = task.Name,
                ["dueAt"] = task.DueAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đặt hạn",
                ["actionUrl"] = Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                    ? origin!.TrimEnd('/') + path
                    : path
            }, cancellationToken);
    }

    // GB-34: mọi thao tác trong phân hiệu của tài khoản đang hoạt động.
    private Task<ulong?> BranchOfAsync(ulong userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.Status == "ACTIVE")
            .Select(user => user.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);

    // GB-54: chỉ người giao (cũng là người kiểm duyệt) và người thực hiện thấy nhiệm vụ.
    private IQueryable<WorkTask> Involving(ulong userId) =>
        db.WorkTasks.AsNoTracking().Where(task => task.CreatedByUserId == userId || task.AssignedToUserId == userId);

    private IQueryable<User> PeopleWithRoles(ulong branchId, IReadOnlyCollection<string> roleCodes) =>
        db.Users.AsNoTracking().Where(user =>
            user.Status == "ACTIVE" &&
            user.SchoolBranchId == branchId &&
            db.UserRoles.WhereEffective().Any(userRole => userRole.UserId == user.Id && roleCodes.Contains(userRole.Role.Code)));

    private Task<List<TaskPersonOption>> PersonOptionsAsync(
        ulong branchId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken) =>
        PeopleWithRoles(branchId, roleCodes)
            .OrderBy(user => user.FullName)
            .Select(user => new TaskPersonOption(user.Id, user.FullName, user.Username))
            .ToListAsync(cancellationToken);

    // "Chưa bắt đầu" (GB-55, GB-56): TaskProgressQuery.WhereNotStarted.
    private async Task<bool> IsStartedAsync(ulong taskId, CancellationToken cancellationToken) =>
        !await db.WorkTasks.Where(task => task.Id == taskId).WhereNotStarted(db).AnyAsync(cancellationToken);

    // Cùng quy tắc với TaskRules.IsOverdue, viết cho câu truy vấn.
    private static Expression<Func<WorkTask, bool>> OverdueBefore(DateTime today) => task =>
        (task.Status == MatrixTaskStatusCodes.Assigned || task.Status == MatrixTaskStatusCodes.InProgress ||
         task.Status == MatrixTaskStatusCodes.NeedsRevision) &&
        task.DueAt != null && task.DueAt < today;

    private static bool CanEdit(TaskActor actor, WorkTask task) =>
        task.CreatedByUserId == actor.UserId && task.Status != MatrixTaskStatusCodes.Completed;

    private static bool CanDelete(TaskActor actor, WorkTask task) =>
        task.CreatedByUserId == actor.UserId && task.Status == MatrixTaskStatusCodes.Assigned;

    // MatrixPeopleResolver chỉ gắn nhãn vai trò ma trận; người làm và người duyệt nhiệm vụ câu hỏi là giáo viên.
    private static MatrixPerson? Person(IReadOnlyDictionary<ulong, MatrixPerson> people, ulong userId, WorkTask task) =>
        people.GetValueOrDefault(userId) is { RoleLabel: null } person && task.TaskType == TaskTypeCodes.Question
            ? person with { RoleLabel = TeacherLabel }
            : people.GetValueOrDefault(userId);

    private static uint Count(QuestionTask lesson, string level) =>
        lesson.Details.Where(detail => detail.CognitiveLevel == level).Select(detail => detail.QuestionCount).FirstOrDefault();

    private static ServiceResult<TaskDetail> StartedFailure() => ServiceResult<TaskDetail>.Failure(
        TaskErrorCodes.Started,
        "Người thực hiện đã bắt đầu nên không đổi được người thực hiện, phạm vi hay nội dung cần làm. Chỉ sửa được nội dung nhiệm vụ và hạn.");

    private static ServiceResult<T> Invalid<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(TaskErrorCodes.Validation, "Vui lòng kiểm tra lại các ô được đánh dấu.", errors);

    private static ServiceResult<T> BranchRequired<T>() =>
        ServiceResult<T>.Failure(TaskErrorCodes.BranchRequired, "Tài khoản chưa được gán phân hiệu nên không dùng được chức năng nhiệm vụ.");

    private static ServiceResult<T> Forbidden<T>(string message) => ServiceResult<T>.Failure(TaskErrorCodes.Forbidden, message);

    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Failure(TaskErrorCodes.NotFound, "Không tìm thấy nhiệm vụ.");
}
