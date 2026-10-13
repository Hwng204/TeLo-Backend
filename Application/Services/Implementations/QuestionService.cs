using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed class QuestionService(ApplicationDbContext db, TimeProvider time)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    public static DateTime? Utc(DateTime? value) => value is { } date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;

    private async Task<WorkTask> TaskAsync(QuestionActor actor, ulong taskId, bool locked, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor.UserId && x.Status == "ACTIVE", ct);
        if (user is null) throw new QuestionException("FORBIDDEN", "Tài khoản không còn hoạt động.", 403);
        var task = locked
            ? (await db.WorkTasks.FromSqlInterpolated($"SELECT * FROM tasks WHERE id = {taskId} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault()
            : await db.WorkTasks.SingleOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null || task.TaskType != TaskTypeCodes.Question) throw new QuestionException("TASK_NOT_FOUND", "Không tìm thấy nhiệm vụ câu hỏi.", 404);
        if (task.AssignedToUserId != actor.UserId && task.CreatedByUserId != actor.UserId)
            throw new QuestionException("FORBIDDEN", "Bạn không phụ trách nhiệm vụ này.", 403);
        if (user.SchoolBranchId is null || await db.QuestionTasks.AnyAsync(x => x.TaskId == taskId && x.Lesson.Chapter.SchoolBranchId != user.SchoolBranchId, ct))
            throw new QuestionException("FORBIDDEN", "Nhiệm vụ không thuộc phân hiệu của bạn.", 403);
        return task;
    }

    private static void Version(WorkTask task, int version)
    {
        if (task.QuestionVersion != version) throw new QuestionException("QUESTION_CONFLICT", "Gói câu hỏi đã thay đổi. Vui lòng tải lại trước khi tiếp tục.", 409);
    }

    private static void Author(QuestionActor actor, WorkTask task)
    {
        if (!actor.Teacher || task.AssignedToUserId != actor.UserId) throw new QuestionException("FORBIDDEN", "Chỉ giáo viên được giao nhiệm vụ được biên soạn và gửi gói.", 403);
        if (!QuestionRules.Editable(task.Status)) throw new QuestionException("QUESTION_LOCKED", "Gói đang chờ duyệt hoặc đã chốt, không thể chỉnh sửa.", 409);
    }

    private IQueryable<Question> Questions(ulong taskId) => db.Questions.Include(x => x.Options)
        .Where(x => x.QuestionTaskDetail.QuestionTask.TaskId == taskId);

    public static QuestionItem Item(Question q)
    {
        var options = QuestionRules.Keys.Select(key => q.Options.FirstOrDefault(x => x.OptionKey == key)?.Content ?? "").ToArray();
        var correct = q.Options.Where(x => x.IsCorrect).ToArray();
        var answer = correct.Length == 1 ? correct[0].OptionKey : null;
        return new(q.Id, q.QuestionTaskDetailId, q.Content, options, answer, q.AnswerExplanation, QuestionRules.IsComplete(q.Content, options, answer));
    }

    public async Task<QuestionPackage> GetAsync(QuestionActor actor, ulong taskId, CancellationToken ct)
    {
        var task = await TaskAsync(actor, taskId, false, ct);
        var rows = await db.QuestionTasks.AsNoTracking().Include(x => x.Lesson).ThenInclude(x => x.Chapter).ThenInclude(x => x.GradeLevel)
            .Include(x => x.Details).Where(x => x.TaskId == taskId).OrderBy(x => x.Id).ToArrayAsync(ct);
        var questions = (await Questions(taskId).AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(ct)).Select(Item).ToArray();
        var targets = rows.SelectMany(row => row.Details.OrderBy(x => Array.IndexOf(new[] { "NHAN_BIET", "THONG_HIEU", "VAN_DUNG" }, x.CognitiveLevel))
            .Select(d => new QuestionTarget(d.Id, row.LessonId, row.Lesson.Chapter.GradeLevel.Name, row.Lesson.Chapter.Code, row.Lesson.Code,
                row.Lesson.Title, d.CognitiveLevel, d.QuestionCount, questions.Count(q => q.DetailId == d.Id), questions.Count(q => q.DetailId == d.Id && q.IsComplete)))).ToArray();
        var submissions = await db.QuestionPackageSubmissions.AsNoTracking().Where(x => x.TaskId == taskId).OrderByDescending(x => x.Number).ToArrayAsync(ct);
        var people = await db.Users.Where(x => x.Id == task.AssignedToUserId || x.Id == task.CreatedByUserId).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var editable = actor.Teacher && task.AssignedToUserId == actor.UserId && QuestionRules.Editable(task.Status);
        var history = submissions.Select(s => new QuestionSubmission(s.Id, s.Number, Utc(s.SubmittedAt)!.Value, s.Status, s.Comment, Utc(s.ReviewedAt),
            JsonSerializer.Deserialize<QuestionReviewNote[]>(s.NotesJson) ?? [], JsonSerializer.Deserialize<QuestionItem[]>(s.SnapshotJson) ?? [])).ToArray();
        // The reviewer sees a submitted snapshot, never the teacher's ongoing edits.
        var visible = task.AssignedToUserId == actor.UserId || task.Status == "COMPLETED" ? questions : history.FirstOrDefault()?.Questions.ToArray() ?? [];
        return new(taskId, task.Name ?? "Gói câu hỏi", task.Status, task.QuestionVersion, task.DueAt,
            people.GetValueOrDefault(task.AssignedToUserId, ""), people.GetValueOrDefault(task.CreatedByUserId, ""), editable,
            editable && QuestionRules.Ready(targets.Select(x => ((int)x.RequiredCount, x.ValidCount))) && questions.All(x => x.IsComplete),
            actor.TeamLead && task.CreatedByUserId == actor.UserId && task.Status == "PENDING_REVIEW", targets, visible, history);
    }

    public async Task<QuestionPackage> SaveAsync(QuestionActor actor, ulong taskId, ulong? id, SaveQuestionRequest request, CancellationToken ct)
    {
        var errors = QuestionRules.DraftErrors(request);
        if (errors.Count > 0) throw new QuestionException("VALIDATION_ERROR", "Vui lòng kiểm tra dữ liệu câu hỏi.", 422, errors);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var task = await TaskAsync(actor, taskId, true, ct);
        Author(actor, task); Version(task, request.Version);
        var target = await db.QuestionTaskDetails.SingleOrDefaultAsync(x => x.Id == request.DetailId && x.QuestionTask.TaskId == taskId, ct)
            ?? throw new QuestionException("VALIDATION_ERROR", "Bài học hoặc mức nhận thức không thuộc nhiệm vụ.");
        var q = id is null ? new Question { CreatedByUserId = actor.UserId, Status = "DRAFT" }
            : await Questions(taskId).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new QuestionException("QUESTION_NOT_FOUND", "Không tìm thấy câu hỏi.", 404);
        if (await Questions(taskId).CountAsync(x => x.QuestionTaskDetailId == target.Id && (id == null || x.Id != id), ct) >= target.QuestionCount)
            throw new QuestionException("QUESTION_QUOTA", "Mức nhận thức này đã đủ số câu được giao.", 409);
        q.QuestionTaskDetailId = target.Id;
        q.Content = request.Content?.Trim() ?? "";
        q.AnswerExplanation = string.IsNullOrWhiteSpace(request.Explanation) ? null : request.Explanation.Trim();
        if (id is null) db.Questions.Add(q);
        // Update options in place to preserve references from any existing answer records.
        for (var i = 0; i < 4; i++)
        {
            var key = QuestionRules.Keys[i];
            var option = q.Options.FirstOrDefault(x => x.OptionKey == key);
            if (option is null) { option = new QuestionOption { OptionKey = key, SortOrder = (uint)i }; q.Options.Add(option); }
            option.Content = request.Options![i].Trim(); option.IsCorrect = key == request.CorrectOption;
        }
        Touch(task, actor.UserId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, ct);
    }

    public async Task<QuestionPackage> DeleteAsync(QuestionActor actor, ulong taskId, ulong id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var task = await TaskAsync(actor, taskId, true, ct); Author(actor, task); Version(task, version);
        var q = await Questions(taskId).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new QuestionException("QUESTION_NOT_FOUND", "Không tìm thấy câu hỏi.", 404);
        db.QuestionOptions.RemoveRange(q.Options); db.Questions.Remove(q); Touch(task, actor.UserId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, ct);
    }

    private void Touch(WorkTask task, ulong actorId)
    {
        task.QuestionVersion++; task.UpdatedAt = Now; task.UpdatedByUserId = actorId;
        if (task.Status == "ASSIGNED") task.Status = "IN_PROGRESS";
    }

    public async Task<QuestionPackage> SubmitAsync(QuestionActor actor, ulong taskId, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var task = await TaskAsync(actor, taskId, true, ct);
        if (actor.Teacher && task.AssignedToUserId == actor.UserId && task.Status == "PENDING_REVIEW" && task.QuestionVersion == version + 1)
            return await GetAsync(actor, taskId, ct); // Same submission retried after a lost response.
        Author(actor, task); Version(task, version);
        var package = await GetAsync(actor, taskId, ct);
        if (!package.CanSubmit) throw new QuestionException("QUESTION_INCOMPLETE", "Gói chưa đủ câu hợp lệ theo từng bài học và mức nhận thức.");
        var number = (await db.QuestionPackageSubmissions.Where(x => x.TaskId == taskId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
        db.QuestionPackageSubmissions.Add(new() { TaskId = taskId, Number = number, Version = version,
            SubmittedAt = Now, SnapshotJson = JsonSerializer.Serialize(package.Questions) });
        foreach (var q in await Questions(taskId).ToArrayAsync(ct)) { q.Status = "SUBMITTED"; q.SubmittedAt = Now; q.ReviewedAt = null; }
        task.Status = "PENDING_REVIEW"; Touch(task, actor.UserId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, ct);
    }

    public async Task<QuestionPackage> ReviewAsync(QuestionActor actor, ulong taskId, bool approve, ReviewQuestionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var task = await TaskAsync(actor, taskId, true, ct);
        if (!actor.TeamLead || task.CreatedByUserId != actor.UserId) throw new QuestionException("FORBIDDEN", "Chỉ tổ trưởng giao nhiệm vụ được kiểm duyệt gói.", 403);
        var outcome = approve ? "COMPLETED" : "NEEDS_REVISION";
        if (task.Status == outcome && task.QuestionVersion == request.Version + 1) return await GetAsync(actor, taskId, ct);
        Version(task, request.Version);
        if (task.Status != "PENDING_REVIEW") throw new QuestionException("QUESTION_LOCKED", "Gói không ở trạng thái chờ kiểm duyệt.", 409);
        if ((!approve && string.IsNullOrWhiteSpace(request.Comment)) || request.Comment?.Length > 1000)
            throw new QuestionException("VALIDATION_ERROR", "Nhập nhận xét yêu cầu chỉnh sửa, tối đa 1.000 ký tự.");
        var questions = await Questions(taskId).Include(x => x.QuestionTaskDetail).ThenInclude(x => x.QuestionTask)
            .ThenInclude(x => x.Lesson).ThenInclude(x => x.Chapter).ThenInclude(x => x.Field).ToArrayAsync(ct);
        var notes = request.Notes ?? [];
        if (notes.Count > questions.Length || notes.Any(n => n is null || !questions.Any(q => q.Id == n.QuestionId) || string.IsNullOrWhiteSpace(n.Comment) || n.Comment.Length > 1000))
            throw new QuestionException("VALIDATION_ERROR", "Nhận xét câu hỏi không hợp lệ.");
        var submission = await db.QuestionPackageSubmissions.SingleAsync(x => x.TaskId == taskId && x.Status == "PENDING_REVIEW", ct);
        submission.Status = approve ? "APPROVED" : "REJECTED"; submission.Comment = request.Comment?.Trim();
        submission.NotesJson = JsonSerializer.Serialize(notes); submission.ReviewedAt = Now; submission.ReviewedByUserId = actor.UserId;
        if (approve)
        {
            // Serialise bank creation by branch so two reviewers cannot insert the same unique scope.
            var branchId = questions[0].QuestionTaskDetail.QuestionTask.Lesson.Chapter.SchoolBranchId;
            await db.SchoolBranches.FromSqlInterpolated($"SELECT * FROM school_branches WHERE id = {branchId} FOR UPDATE").ToArrayAsync(ct);
            foreach (var q in questions)
            {
                var chapter = q.QuestionTaskDetail.QuestionTask.Lesson.Chapter;
                q.QuestionBankId = (await BankAsync(chapter.SchoolBranchId, chapter.Field.SubjectId, chapter.GradeLevelId, "exam", ct)).Id;
                q.Status = "APPROVED"; q.ReviewedAt = Now; q.ReviewedByUserId = actor.UserId;
            }
        }
        else foreach (var q in questions) { q.Status = "DRAFT"; q.ReviewedAt = Now; q.ReviewComment = request.Comment?.Trim(); }
        task.Status = outcome; Touch(task, actor.UserId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, ct);
    }

    public async Task<Domain.Entities.QuestionBank.QuestionBank> BankAsync(ulong branchId, ulong subjectId, ulong gradeId, string type, CancellationToken ct)
    {
        var bank = await db.QuestionBanks.SingleOrDefaultAsync(x => x.SchoolBranchId == branchId && x.SubjectId == subjectId && x.GradeLevelId == gradeId && x.BankType == type, ct);
        if (bank is not null) return bank;
        bank = new() { SchoolBranchId = branchId, SubjectId = subjectId, GradeLevelId = gradeId, BankType = type, Status = "ACTIVE" };
        db.QuestionBanks.Add(bank); await db.SaveChangesAsync(ct); return bank;
    }

    public async Task<QuestionPackage> ImportAsync(QuestionActor actor, ulong taskId, int version, IReadOnlyList<SaveQuestionRequest> rows, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var task = await TaskAsync(actor, taskId, true, ct); Author(actor, task); Version(task, version);
        var targets = await db.QuestionTaskDetails.Where(x => x.QuestionTask.TaskId == taskId).ToArrayAsync(ct);
        var existing = await Questions(taskId).Select(x => x.QuestionTaskDetailId).ToArrayAsync(ct);
        if (rows.Count == 0 || rows.Any(r => QuestionRules.DraftErrors(r).Count > 0 || !QuestionRules.IsComplete(r.Content, r.Options, r.CorrectOption) || !targets.Any(t => t.Id == r.DetailId)))
            throw new QuestionException("IMPORT_INVALID", "Tệp chứa câu hỏi không hợp lệ.");
        if (targets.Any(t => existing.Count(x => x == t.Id) + rows.Count(x => x.DetailId == t.Id) > t.QuestionCount))
            throw new QuestionException("QUESTION_QUOTA", "Số câu sau khi nhập vượt yêu cầu nhiệm vụ.", 409);
        foreach (var row in rows)
        {
            var q = new Question { QuestionTaskDetailId = row.DetailId, Content = row.Content!.Trim(), AnswerExplanation = row.Explanation?.Trim(), Status = "DRAFT", CreatedByUserId = actor.UserId };
            for (var i = 0; i < 4; i++) q.Options.Add(new() { OptionKey = QuestionRules.Keys[i], SortOrder = (uint)i, Content = row.Options![i].Trim(), IsCorrect = row.CorrectOption == QuestionRules.Keys[i] });
            db.Questions.Add(q);
        }
        Touch(task, actor.UserId); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, ct);
    }
}
