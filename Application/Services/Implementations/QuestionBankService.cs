using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed class QuestionBankService(ApplicationDbContext db, TaskJournal journal, TimeProvider time, QuestionAccess access)
{
    private Task<User> UserAsync(QuestionActor actor, CancellationToken ct) => access.UserAsync(actor, ct);

    private async Task<IQueryable<Question>> ScopedAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        if (user.SchoolBranchId is null && !actor.Principal) throw new QuestionException("BRANCH_REQUIRED", "Tài khoản chưa được gắn phân hiệu.", 403);
        var query = db.Questions.Where(x => x.Status == "APPROVED");
        // A branchless Principal has the existing global read scope used by the matrix module.
        if (actor.Principal && user.SchoolBranchId is null) return query;
        return actor.Principal ? query.Where(x => x.Lesson.Chapter.SchoolBranch.SchoolId == user.SchoolBranch!.SchoolId)
            : query.Where(x => x.Lesson.Chapter.SchoolBranchId == user.SchoolBranchId);
    }

    private IQueryable<ulong> ProtectedIds() => db.ExamSetQuestions.Where(q => db.ExamSubjects.Any(link =>
        link.ExamSetId == q.ExamSetId && db.Exams.Any(exam => exam.Id == link.ExamId &&
        (exam.Status == "SCHEDULED" || exam.Status == "ONGOING")))).Select(x => x.QuestionId);

    public static IQueryable<Question> Include(IQueryable<Question> query) => query.Include(x => x.Options).Include(x => x.CreatedByUser)
        .Include(x => x.Lesson).ThenInclude(x => x.Chapter).ThenInclude(x => x.Subject)
        .Include(x => x.Lesson).ThenInclude(x => x.Chapter).ThenInclude(x => x.GradeLevel)
        .Include(x => x.QuestionTaskDetail).ThenInclude(x => x.QuestionTask);

    private sealed record PracticeMove(string Reason);
    private sealed record ReviewMetadata(string Status);
    private async Task<BankQuestion[]> ItemsAsync(Question[] questions, bool pht, ulong[] protectedIds, CancellationToken ct)
    {
        var taskIds = questions.Select(q => q.QuestionTaskDetail.QuestionTask.TaskId).Distinct().ToArray();
        var history = await db.TaskHistories.AsNoTracking().Where(row => taskIds.Contains(row.TaskId)).ToArrayAsync(ct);
        var approvedAt = taskIds.ToDictionary(task => task, task => TaskJournalCodec.Decode(history.Where(row => row.TaskId == task), "question")
            .LastOrDefault(entry => entry.Action == "REVIEWED" && entry.Read<ReviewMetadata>().Status == "APPROVED")?.At);
        return questions.Select(q =>
        {
            var task = q.QuestionTaskDetail.QuestionTask.TaskId;
            var moved = TaskJournalCodec.Decode(history.Where(row => row.TaskId == task), "bank:" + q.Id, latestOnly: true).LastOrDefault();
            return new BankQuestion(QuestionService.Item(q), q.BankType, q.LessonId, q.Lesson.Title,
                q.QuestionTaskDetail.CognitiveLevel, q.Lesson.Chapter.Subject.Name, q.Lesson.Chapter.GradeLevel.Name,
                q.CreatedByUser.FullName, approvedAt[task], pht && q.BankType == "exam" && !protectedIds.Contains(q.Id),
                moved?.At, moved?.Read<PracticeMove>().Reason);
        }).ToArray();
    }

    public async Task<DirectoryPage<BankQuestion>> ListAsync(QuestionActor actor, QuestionBankQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 100000 || request.PageSize is < 1 or > 100 || request.BankType is not ("exam" or "practice") ||
            request.Keyword?.Length > 255 || request.CognitiveLevel is not null && !MatrixCognitiveLevels.IsKnown(request.CognitiveLevel))
            throw new QuestionException("VALIDATION_ERROR", "Bộ lọc ngân hàng câu hỏi không hợp lệ.");
        var q = (await ScopedAsync(actor, ct)).Where(x => x.BankType == request.BankType);
        if (!string.IsNullOrWhiteSpace(request.Keyword)) q = q.Where(x => x.Content.Contains(request.Keyword.Trim()));
        if (request.SubjectId is { } subject) q = q.Where(x => x.Lesson.Chapter.SubjectId == subject);
        if (request.GradeLevelId is { } grade) q = q.Where(x => x.Lesson.Chapter.GradeLevelId == grade);
        if (request.ChapterId is { } chapter) q = q.Where(x => x.QuestionTaskDetail.QuestionTask.Lesson.ChapterId == chapter);
        if (request.LessonId is { } lesson) q = q.Where(x => x.QuestionTaskDetail.QuestionTask.LessonId == lesson);
        if (request.CognitiveLevel is { } level) q = q.Where(x => x.QuestionTaskDetail.CognitiveLevel == level);
        if (request.MatrixId is { } matrixId)
        {
            var matrix = await MatrixAsync(actor, matrixId, ct);
            q = q.Where(x => x.Lesson.Chapter.SchoolBranchId == matrix.BranchId && x.Lesson.Chapter.SubjectId == matrix.SubjectId &&
                x.Lesson.Chapter.GradeLevelId == matrix.GradeLevelId && x.Options.Count == 4 && x.Options.All(option => option.Content.Trim() != "") &&
                x.Options.Count(option => option.IsCorrect) == 1 && x.Content.Trim() != "" && x.Lesson.Status == "ACTIVE" && x.Lesson.Chapter.Status == "ACTIVE");
            if (request.MatrixDetailId is { } detailId)
            {
                var d = matrix.Details.SingleOrDefault(x => x.Id == detailId) ?? throw new QuestionException("VALIDATION_ERROR", "Ô không thuộc ma trận.");
                q = q.Where(x => x.LessonId == d.LessonId && x.QuestionTaskDetail.CognitiveLevel == d.CognitiveLevel && x.QuestionTaskDetail.QuestionType == d.QuestionType);
            }
            else q = q.Where(x => db.MatrixDetails.Any(d => d.ExamMatrixId == matrixId && d.LessonId == x.QuestionTaskDetail.QuestionTask.LessonId && d.CognitiveLevel == x.QuestionTaskDetail.CognitiveLevel && d.QuestionType == x.QuestionTaskDetail.QuestionType));
        }
        var total = await q.CountAsync(ct);
        var rows = await Include(q).AsNoTracking().OrderByDescending(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var protectedIds = await ProtectedIds().Where(x => ids.Contains(x)).ToArrayAsync(ct);
        return new(await ItemsAsync(rows, actor.Pht, protectedIds, ct), request.Page, request.PageSize, total);
    }

    public async Task<BankQuestion> DetailAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var q = await Include(await ScopedAsync(actor, ct)).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QuestionException("QUESTION_NOT_FOUND", "Không tìm thấy câu hỏi.", 404);
        return (await ItemsAsync([q], actor.Pht, await ProtectedIds().Where(x => x == id).ToArrayAsync(ct), ct))[0];
    }

    public async Task<QuestionBankOptions> OptionsAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        if (user.SchoolBranchId is null && !actor.Principal) throw new QuestionException("BRANCH_REQUIRED", "Tài khoản chưa được gắn phân hiệu.", 403);
        var chapters = db.Chapters.AsNoTracking();
        if (!actor.Principal) chapters = chapters.Where(x => x.SchoolBranchId == user.SchoolBranchId);
        else if (user.SchoolBranchId is not null) { var schoolId = user.SchoolBranch!.SchoolId; chapters = chapters.Where(x => x.SchoolBranch.SchoolId == schoolId); }
        var subjects = await chapters.Select(x => new { Id = x.SubjectId, Name = x.Subject.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        var grades = await chapters.Select(x => new { Id = x.GradeLevelId, Name = x.GradeLevel.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        return new(subjects.Select(x => new BankFilterOption(x.Id, x.Name)).ToArray(),
            grades.Select(x => new BankFilterOption(x.Id, x.Name)).ToArray(),
            await chapters.OrderBy(x => x.Id).Select(x => new BankFilterOption(x.Id, x.Title, x.GradeLevelId)).ToArrayAsync(ct),
            await db.Lessons.Where(x => chapters.Any(c => c.Id == x.ChapterId)).OrderBy(x => x.Id).Select(x => new BankFilterOption(x.Id, x.Title, x.ChapterId)).ToArrayAsync(ct));
    }

    private async Task<ExamMatrix> MatrixAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        var matrix = await db.ExamMatrices.AsNoTracking().Include(x => x.Details).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QuestionException("MATRIX_NOT_FOUND", "Không tìm thấy ma trận.", 404);
        var scopeMatches = actor.Principal && user.SchoolBranchId is null || user.SchoolBranchId == matrix.BranchId;
        if (!scopeMatches && actor.Principal && user.SchoolBranch is not null)
        {
            var schoolId = user.SchoolBranch.SchoolId;
            scopeMatches = await db.SchoolBranches.AnyAsync(x => x.Id == matrix.BranchId && x.SchoolId == schoolId, ct);
        }
        var teacherOnly = actor.Teacher && !actor.Pht && !actor.Principal && !actor.TeamLead;
        if (!scopeMatches || teacherOnly && matrix.Status is not ("APPROVED" or "ARCHIVED") ||
            matrix.TaskId != null && matrix.Status == "DRAFT" && matrix.CreatedByUserId != actor.UserId)
            throw new QuestionException("FORBIDDEN", "Bạn không có quyền xem ma trận này.", 403);
        return matrix;
    }

    public async Task<MatrixQuestionAvailability[]> AvailabilityAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var matrix = await MatrixAsync(actor, id, ct);
        var questions = (await ScopedAsync(actor, ct)).Where(x => x.BankType == "exam" && x.Lesson.Chapter.SubjectId == matrix.SubjectId && x.Lesson.Chapter.GradeLevelId == matrix.GradeLevelId);
        var candidates = await Include(questions).AsNoTracking().ToArrayAsync(ct);
        return matrix.Details.Select(cell => new MatrixQuestionAvailability(cell.Id, cell.LessonId, cell.CognitiveLevel, cell.QuestionCount,
            candidates.Count(q => ExamPackageRules.Matches(q, matrix, cell)))).ToArray();
    }

    public async Task<BankQuestion> MoveAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        if (!actor.Pht) throw new QuestionException("FORBIDDEN", "Chỉ Phó Hiệu trưởng được chuyển câu hỏi sang ôn tập.", 403);
        await DetailAsync(actor, id, ct);
        var branchId = (await UserAsync(actor, ct)).SchoolBranchId;
        if (branchId is null || !await db.Questions.AnyAsync(q => q.Id == id && q.Lesson.Chapter.SchoolBranchId == branchId, ct))
            throw new QuestionException("FORBIDDEN", "Chỉ chuyển câu hỏi thuộc phân hiệu của bạn.", 403);
        await MoveInternalAsync(id, actor.UserId, "PHT chuyển thủ công", ct, actor);
        return await DetailAsync(actor, id, ct);
    }

    private async Task MoveInternalAsync(ulong id, ulong? actorId, string reason, CancellationToken ct, QuestionActor? actor = null)
    {
        var branchId = await db.Questions.Where(x => x.Id == id).Select(x => x.Lesson.Chapter.SchoolBranchId).SingleAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await db.SchoolBranches.FromSqlInterpolated($"SELECT * FROM branch WHERE branch_id = {branchId} FOR UPDATE").ToArrayAsync(ct);
        if (actor is not null && (await UserAsync(actor, ct)).SchoolBranchId != branchId)
            throw new QuestionException("FORBIDDEN", "Phân hiệu đã thay đổi.", 403);
        var row = (await db.Questions.FromSqlInterpolated($"SELECT * FROM question WHERE question_id = {id} FOR UPDATE").ToArrayAsync(ct)).Single();
        if (row.BankType == "practice") return;
        if (row.Status != "APPROVED" || await ProtectedIds().ContainsAsync(id, ct))
            throw new QuestionException("QUESTION_IN_USE", "Câu hỏi đang thuộc đề của kỳ thi chưa kết thúc.", 409);
        await db.Entry(row).Reference(x => x.QuestionTaskDetail).Query().Include(x => x.QuestionTask).LoadAsync(ct);
        var taskId = row.QuestionTaskDetail.QuestionTask.TaskId;
        var events = await journal.ReadAsync(taskId, "bank:" + id, ct);
        row.BankType = "practice";
        await journal.AppendAsync(taskId, "bank:" + id, events.Count, "MOVED_TO_PRACTICE", new PracticeMove(reason), actorId ?? row.CreatedByUserId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> ReleaseCompletedAsync(CancellationToken ct)
    {
        // Only variants with actual attempt/result records in completed exams
        // release their used questions. An unused backup variant is excluded.
        var usedQuestions = new Dictionary<ulong, string>();
        ulong cursor = 0;
        while (usedQuestions.Count < 200)
        {
            var batch = await db.ExamVariants.AsNoTracking().Where(variant => variant.Id > cursor &&
                db.TestResults.Any(result => result.ExamVariantId == variant.Id && result.Exam.Status == "COMPLETED") &&
                db.ExamSetQuestions.Any(row => row.ExamSetId == variant.ExamSetId && row.Question.Status == "APPROVED" &&
                    row.Question.BankType == "exam" && !ProtectedIds().Contains(row.QuestionId)))
                .OrderBy(variant => variant.Id).Take(100).Select(variant => new
                {
                    variant.Id, variant.ExamSetId, variant.QuestionOrder,
                    Name = db.TestResults.Where(result => result.ExamVariantId == variant.Id && result.Exam.Status == "COMPLETED")
                        .OrderBy(result => result.Id).Select(result => result.Exam.Name).FirstOrDefault()
                }).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            cursor = batch[^1].Id;
            var setIds = batch.Select(variant => variant.ExamSetId).Distinct().ToArray();
            var eligible = await db.ExamSetQuestions.AsNoTracking().Where(row => setIds.Contains(row.ExamSetId) &&
                row.Question.Status == "APPROVED" && row.Question.BankType == "exam" && !ProtectedIds().Contains(row.QuestionId))
                .Select(row => new { row.ExamSetId, row.QuestionId }).ToArrayAsync(ct);
            var bySet = eligible.ToLookup(row => row.ExamSetId, row => row.QuestionId);
            foreach (var variant in batch)
            {
                ulong[] ids;
                try { ids = System.Text.Json.JsonSerializer.Deserialize<ulong[]>(variant.QuestionOrder) ?? []; }
                catch (System.Text.Json.JsonException) { continue; } // Unknown external format never releases questions.
                foreach (var id in bySet[variant.ExamSetId].Intersect(ids))
                {
                    usedQuestions.TryAdd(id, variant.Name ?? "");
                    if (usedQuestions.Count == 200) break;
                }
                if (usedQuestions.Count == 200) break;
            }
        }
        var idsUsed = usedQuestions.Keys.ToArray();
        var candidates = await db.Questions.AsNoTracking().Where(q => idsUsed.Contains(q.Id) && q.Status == "APPROVED" && q.BankType == "exam" && !ProtectedIds().Contains(q.Id))
            .OrderBy(q => q.Id).Select(q => q.Id).Take(200).ToArrayAsync(ct);
        var moved = 0;
        foreach (var id in candidates)
        {
            try { await MoveInternalAsync(id, null, "Tự chuyển sau kỳ thi: " + usedQuestions[id], ct); moved++; }
            catch (QuestionException error) when (error.Code == "QUESTION_IN_USE") { }
            db.ChangeTracker.Clear();
        }
        return moved;
    }

    public async Task<int> OverdueAsync(QuestionActor actor, CancellationToken ct)
    {
        if (!actor.TeamLead) throw new QuestionException("FORBIDDEN", "Chỉ tổ trưởng xem cảnh báo này.", 403);
        await UserAsync(actor, ct);
        var today = TaskRules.Today(time).ToDateTime(TimeOnly.MinValue);
        return await db.WorkTasks.CountAsync(x => x.TaskType == "QUESTION" && x.CreatedByUserId == actor.UserId && x.DueAt < today &&
            (x.Status == "ASSIGNED" || x.Status == "IN_PROGRESS" || x.Status == "NEEDS_REVISION"), ct);
    }
}
