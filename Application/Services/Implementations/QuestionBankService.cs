using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed class QuestionBankService(ApplicationDbContext db, QuestionService packages, TimeProvider time)
{
    private async Task<User> UserAsync(QuestionActor actor, CancellationToken ct) =>
        await db.Users.AsNoTracking().Include(x => x.SchoolBranch).SingleOrDefaultAsync(x => x.Id == actor.UserId && x.Status == "ACTIVE", ct)
        ?? throw new QuestionException("FORBIDDEN", "Tài khoản không còn hoạt động.", 403);

    private async Task<IQueryable<Question>> ScopedAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        if (user.SchoolBranchId is null && !actor.Principal) throw new QuestionException("BRANCH_REQUIRED", "Tài khoản chưa được gắn phân hiệu.", 403);
        var query = db.Questions.Where(x => x.Status == "APPROVED" && x.QuestionBankId != null);
        // A branchless Principal has the existing global read scope used by the matrix module.
        if (actor.Principal && user.SchoolBranchId is null) return query;
        return actor.Principal ? query.Where(x => x.QuestionBank!.SchoolBranch.SchoolId == user.SchoolBranch!.SchoolId)
            : query.Where(x => x.QuestionBank!.SchoolBranchId == user.SchoolBranchId);
    }

    private IQueryable<ulong> ProtectedIds() => db.ExamSetQuestions.Where(q => db.ExamSubjectGradeLevels.Any(g =>
        (g.PrimaryExamSetId == q.ExamSetId || g.BackupExamSetId == q.ExamSetId) &&
        (g.ExamSubject.Exam.Status == "SCHEDULED" || g.ExamSubject.Exam.Status == "ONGOING"))).Select(x => x.QuestionId);

    private static IQueryable<Question> Include(IQueryable<Question> q) => q.Include(x => x.Options).Include(x => x.CreatedByUser)
        .Include(x => x.QuestionBank).ThenInclude(x => x!.Subject).Include(x => x.QuestionBank).ThenInclude(x => x!.GradeLevel)
        .Include(x => x.QuestionTaskDetail).ThenInclude(x => x.QuestionTask).ThenInclude(x => x.Lesson);

    private static BankQuestion Item(Question q, bool canMove) => new(QuestionService.Item(q), q.QuestionBank!.BankType,
        q.QuestionTaskDetail.QuestionTask.LessonId, q.QuestionTaskDetail.QuestionTask.Lesson.Title, q.QuestionTaskDetail.CognitiveLevel,
        q.QuestionBank.Subject.Name, q.QuestionBank.GradeLevel.Name, q.CreatedByUser.FullName, QuestionService.Utc(q.ReviewedAt),
        canMove && q.QuestionBank.BankType == "exam", QuestionService.Utc(q.PracticeAt), q.PracticeReason);

    public async Task<DirectoryPage<BankQuestion>> ListAsync(QuestionActor actor, QuestionBankQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 100000 || request.PageSize is < 1 or > 100 || request.BankType is not ("exam" or "practice") ||
            request.Keyword?.Length > 255 || request.CognitiveLevel is not null && !MatrixCognitiveLevels.IsKnown(request.CognitiveLevel))
            throw new QuestionException("VALIDATION_ERROR", "Bộ lọc ngân hàng câu hỏi không hợp lệ.");
        var q = (await ScopedAsync(actor, ct)).Where(x => x.QuestionBank!.BankType == request.BankType);
        if (!string.IsNullOrWhiteSpace(request.Keyword)) q = q.Where(x => x.Content.Contains(request.Keyword.Trim()));
        if (request.SubjectId is { } subject) q = q.Where(x => x.QuestionBank!.SubjectId == subject);
        if (request.GradeLevelId is { } grade) q = q.Where(x => x.QuestionBank!.GradeLevelId == grade);
        if (request.ChapterId is { } chapter) q = q.Where(x => x.QuestionTaskDetail.QuestionTask.Lesson.ChapterId == chapter);
        if (request.LessonId is { } lesson) q = q.Where(x => x.QuestionTaskDetail.QuestionTask.LessonId == lesson);
        if (request.CognitiveLevel is { } level) q = q.Where(x => x.QuestionTaskDetail.CognitiveLevel == level);
        if (request.MatrixId is { } matrixId)
        {
            var matrix = await MatrixAsync(actor, matrixId, ct);
            q = q.Where(x => x.QuestionBank!.SubjectId == matrix.AcademicContext.SubjectId && x.QuestionBank.GradeLevelId == matrix.AcademicContext.GradeLevelId);
            if (request.MatrixDetailId is { } detailId)
            {
                var d = matrix.Details.SingleOrDefault(x => x.Id == detailId) ?? throw new QuestionException("VALIDATION_ERROR", "Ô không thuộc ma trận.");
                q = q.Where(x => x.QuestionTaskDetail.QuestionTask.LessonId == d.LessonId && x.QuestionTaskDetail.CognitiveLevel == d.CognitiveLevel);
            }
            else q = q.Where(x => db.MatrixDetails.Any(d => d.ExamMatrixId == matrixId && d.LessonId == x.QuestionTaskDetail.QuestionTask.LessonId && d.CognitiveLevel == x.QuestionTaskDetail.CognitiveLevel));
        }
        var total = await q.CountAsync(ct);
        var rows = await Include(q).AsNoTracking().OrderByDescending(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var protectedIds = await ProtectedIds().Where(x => ids.Contains(x)).ToArrayAsync(ct);
        return new(rows.Select(x => Item(x, actor.Pht && !protectedIds.Contains(x.Id))).ToArray(), request.Page, request.PageSize, total);
    }

    public async Task<BankQuestion> DetailAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var q = await Include(await ScopedAsync(actor, ct)).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QuestionException("QUESTION_NOT_FOUND", "Không tìm thấy câu hỏi.", 404);
        return Item(q, actor.Pht && !await ProtectedIds().ContainsAsync(id, ct));
    }

    public async Task<QuestionBankOptions> OptionsAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        if (user.SchoolBranchId is null && !actor.Principal) throw new QuestionException("BRANCH_REQUIRED", "Tài khoản chưa được gắn phân hiệu.", 403);
        var chapters = db.Chapters.AsNoTracking();
        if (!actor.Principal) chapters = chapters.Where(x => x.SchoolBranchId == user.SchoolBranchId);
        else if (user.SchoolBranchId is not null) { var schoolId = user.SchoolBranch!.SchoolId; chapters = chapters.Where(x => x.SchoolBranch.SchoolId == schoolId); }
        var subjects = await chapters.Select(x => new { Id = x.Field.SubjectId, Name = x.Field.Subject.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        var grades = await chapters.Select(x => new { Id = x.GradeLevelId, Name = x.GradeLevel.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        return new(subjects.Select(x => new BankFilterOption(x.Id, x.Name)).ToArray(),
            grades.Select(x => new BankFilterOption(x.Id, x.Name)).ToArray(),
            await chapters.OrderBy(x => x.SortOrder).Select(x => new BankFilterOption(x.Id, x.Code + " · " + x.Title, x.GradeLevelId)).ToArrayAsync(ct),
            await db.Lessons.Where(x => chapters.Any(c => c.Id == x.ChapterId)).OrderBy(x => x.SortOrder).Select(x => new BankFilterOption(x.Id, x.Code + " · " + x.Title, x.ChapterId)).ToArrayAsync(ct));
    }

    private async Task<ExamMatrix> MatrixAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        var matrix = await db.ExamMatrices.AsNoTracking().Include(x => x.AcademicContext).Include(x => x.Details).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new QuestionException("MATRIX_NOT_FOUND", "Không tìm thấy ma trận.", 404);
        var scopeMatches = actor.Principal && user.SchoolBranchId is null || user.SchoolBranchId == matrix.AcademicContext.SchoolBranchId;
        if (!scopeMatches && actor.Principal && user.SchoolBranch is not null)
        {
            var schoolId = user.SchoolBranch.SchoolId;
            scopeMatches = await db.SchoolBranches.AnyAsync(x => x.Id == matrix.AcademicContext.SchoolBranchId && x.SchoolId == schoolId, ct);
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
        var questions = (await ScopedAsync(actor, ct)).Where(x => x.QuestionBank!.BankType == "exam" && x.QuestionBank.SubjectId == matrix.AcademicContext.SubjectId && x.QuestionBank.GradeLevelId == matrix.AcademicContext.GradeLevelId);
        var counts = await questions.GroupBy(x => new { x.QuestionTaskDetail.QuestionTask.LessonId, x.QuestionTaskDetail.CognitiveLevel })
            .Select(x => new { x.Key.LessonId, x.Key.CognitiveLevel, Count = x.Count() }).ToArrayAsync(ct);
        return matrix.Details.Select(x => new MatrixQuestionAvailability(x.Id, x.LessonId, x.CognitiveLevel, x.QuestionCount,
            counts.FirstOrDefault(c => c.LessonId == x.LessonId && c.CognitiveLevel == x.CognitiveLevel)?.Count ?? 0)).ToArray();
    }

    public async Task<BankQuestion> MoveAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        if (!actor.Pht) throw new QuestionException("FORBIDDEN", "Chỉ Phó Hiệu trưởng được chuyển câu hỏi sang ôn tập.", 403);
        await DetailAsync(actor, id, ct);
        await MoveInternalAsync(id, actor.UserId, "PHT chuyển thủ công", ct);
        return await DetailAsync(actor, id, ct);
    }

    private async Task MoveInternalAsync(ulong id, ulong? actorId, string reason, CancellationToken ct)
    {
        var branchId = await db.Questions.Where(x => x.Id == id && x.QuestionBankId != null).Select(x => x.QuestionBank!.SchoolBranchId).SingleAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SchoolBranches.FromSqlInterpolated($"SELECT * FROM school_branches WHERE id = {branchId} FOR UPDATE").ToArrayAsync(ct);
        var row = (await db.Questions.FromSqlInterpolated($"SELECT * FROM questions WHERE id = {id} FOR UPDATE").ToArrayAsync(ct)).Single();
        await db.Entry(row).Reference(x => x.QuestionBank).LoadAsync(ct);
        if (row.QuestionBank!.BankType == "practice") return;
        if (await ProtectedIds().ContainsAsync(id, ct)) throw new QuestionException("QUESTION_IN_USE", "Câu hỏi đang thuộc đề của kỳ thi chưa kết thúc.", 409);
        var bank = await packages.BankAsync(branchId, row.QuestionBank.SubjectId, row.QuestionBank.GradeLevelId, "practice", ct);
        row.QuestionBankId = bank.Id; row.PracticeAt = time.GetUtcNow().UtcDateTime; row.PracticeByUserId = actorId; row.PracticeReason = reason;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<int> ReleaseCompletedAsync(CancellationToken ct)
    {
        var usedIds = db.ExamVariantQuestions.Where(v => db.ExamAttempts.Any(a => a.ExamVariantId == v.ExamVariantId &&
            a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.Status == "COMPLETED")).Select(x => x.ExamSetQuestion.QuestionId);
        var candidates = await db.Questions.AsNoTracking().Where(x => x.Status == "APPROVED" && x.QuestionBank!.BankType == "exam" &&
            usedIds.Contains(x.Id) && !ProtectedIds().Contains(x.Id)).OrderBy(x => x.Id).Select(x => x.Id).Take(200).ToArrayAsync(ct);
        var moved = 0;
        foreach (var id in candidates)
        {
            try
            {
                var source = await db.ExamAttempts.Where(a => a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.Status == "COMPLETED" &&
                    db.ExamVariantQuestions.Any(v => v.ExamVariantId == a.ExamVariantId && v.ExamSetQuestion.QuestionId == id))
                    .OrderBy(a => a.StartedAt).Select(a => a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.Name).FirstAsync(ct);
                var reason = "Tự chuyển sau kỳ thi: " + source;
                await MoveInternalAsync(id, null, reason[..Math.Min(255, reason.Length)], ct); moved++;
            }
            catch (QuestionException ex) when (ex.Code == "QUESTION_IN_USE") { }
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
