using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed record PaperQuestionRequest(int Version, ulong DetailId, IReadOnlyList<ulong>? QuestionIds);
public sealed record PaperQuestionCell(ulong Id, string LessonTitle, string CognitiveLevel, uint RequiredCount);
public sealed record SelectedPaperQuestion(ulong DetailId, QuestionItem Question);
public sealed record PaperQuestions(ulong TaskId, ulong PaperId, string Name, ulong MatrixId, int Version, bool CanEdit,
    IReadOnlyList<PaperQuestionCell> Cells, IReadOnlyList<SelectedPaperQuestion> Questions);

public sealed class QuestionPaperService(ApplicationDbContext db, TimeProvider time)
{
    private async Task<(WorkTask Task, ExamTaskPaper Paper, ExamMatrix Matrix)> LoadAsync(QuestionActor actor, ulong taskId, ulong paperId, bool write, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor.UserId && x.Status == "ACTIVE", ct);
        if (user is null) throw new QuestionException("FORBIDDEN", "Tài khoản không còn hoạt động.", 403);
        var task = write ? (await db.WorkTasks.FromSqlInterpolated($"SELECT * FROM tasks WHERE id = {taskId} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault()
            : await db.WorkTasks.SingleOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null || task.TaskType != "EXAM" || task.ExamMatrixId is null) throw new QuestionException("TASK_NOT_FOUND", "Không tìm thấy nhiệm vụ tạo đề.", 404);
        if (task.AssignedToUserId != actor.UserId && task.CreatedByUserId != actor.UserId) throw new QuestionException("FORBIDDEN", "Bạn không phụ trách nhiệm vụ tạo đề này.", 403);
        var matrix = await db.ExamMatrices.Include(x => x.AcademicContext).Include(x => x.Details).ThenInclude(x => x.Lesson).SingleAsync(x => x.Id == task.ExamMatrixId, ct);
        if (user.SchoolBranchId != matrix.AcademicContext.SchoolBranchId) throw new QuestionException("FORBIDDEN", "Đề không thuộc phân hiệu của bạn.", 403);
        var paper = await db.ExamTaskPapers.SingleOrDefaultAsync(x => x.Id == paperId && x.TaskId == taskId, ct) ?? throw new QuestionException("PAPER_NOT_FOUND", "Không tìm thấy đề được giao.", 404);
        if (write && (!actor.TeamLead || task.AssignedToUserId != actor.UserId || !QuestionRules.Editable(task.Status)))
            throw new QuestionException("FORBIDDEN", "Chỉ tổ trưởng được giao đang biên soạn mới được chọn câu hỏi.", 403);
        if (write && paper.ExamSetId != null && !await db.ExamSets.AnyAsync(x => x.Id == paper.ExamSetId && x.Status == "DRAFT", ct))
            throw new QuestionException("QUESTION_LOCKED", "Đề đã được chốt, không thể thay đổi câu hỏi.", 409);
        return (task, paper, matrix);
    }

    public async Task<PaperQuestions> GetAsync(QuestionActor actor, ulong taskId, ulong paperId, CancellationToken ct)
    {
        var (task, paper, matrix) = await LoadAsync(actor, taskId, paperId, false, ct);
        var rows = await db.ExamSetQuestions.AsNoTracking().Include(x => x.Question).ThenInclude(x => x.Options)
            .Where(x => x.ExamSetId == paper.ExamSetId).OrderBy(x => x.Id).ToArrayAsync(ct);
        var mutableSet = paper.ExamSetId is null || await db.ExamSets.AnyAsync(x => x.Id == paper.ExamSetId && x.Status == "DRAFT", ct);
        return new(taskId, paperId, paper.Name, matrix.Id, task.QuestionVersion,
            mutableSet && actor.TeamLead && task.AssignedToUserId == actor.UserId && QuestionRules.Editable(task.Status),
            matrix.Details.OrderBy(x => x.Id).Select(x => new PaperQuestionCell(x.Id, x.Lesson.Title, x.CognitiveLevel, x.QuestionCount)).ToArray(),
            rows.Select(x => new SelectedPaperQuestion(x.MatrixDetailId ?? 0, QuestionService.Item(x.Question))).ToArray());
    }

    public async Task<PaperQuestions> AddAsync(QuestionActor actor, ulong taskId, ulong paperId, PaperQuestionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (task, paper, matrix) = await LoadAsync(actor, taskId, paperId, true, ct);
        if (task.QuestionVersion != request.Version) throw new QuestionException("QUESTION_CONFLICT", "Nội dung đề đã thay đổi. Vui lòng tải lại.", 409);
        var ids = request.QuestionIds;
        if (ids is null || ids.Count is 0 or > 1000 || ids.Distinct().Count() != ids.Count) throw new QuestionException("VALIDATION_ERROR", "Danh sách câu được chọn không hợp lệ.");
        var cell = matrix.Details.SingleOrDefault(x => x.Id == request.DetailId) ?? throw new QuestionException("VALIDATION_ERROR", "Ô không thuộc ma trận.");
        var branchId = matrix.AcademicContext.SchoolBranchId;
        await db.SchoolBranches.FromSqlInterpolated($"SELECT * FROM school_branches WHERE id = {branchId} FOR UPDATE").ToArrayAsync(ct);
        var candidates = await db.Questions.Where(x => ids.Contains(x.Id) && x.Status == "APPROVED" && x.QuestionBank!.BankType == "exam" &&
            x.QuestionBank.SchoolBranchId == branchId && x.QuestionBank.SubjectId == matrix.AcademicContext.SubjectId && x.QuestionBank.GradeLevelId == matrix.AcademicContext.GradeLevelId &&
            x.QuestionTaskDetail.QuestionTask.LessonId == cell.LessonId && x.QuestionTaskDetail.CognitiveLevel == cell.CognitiveLevel).Select(x => x.Id).ToArrayAsync(ct);
        if (candidates.Length != ids.Count) throw new QuestionException("VALIDATION_ERROR", "Có câu không còn phù hợp ô ma trận hoặc đã chuyển sang ôn tập.");
        var existing = await db.ExamSetQuestions.Where(x => x.ExamSetId == paper.ExamSetId).ToArrayAsync(ct);
        if (existing.Any(x => ids.Contains(x.QuestionId) && x.MatrixDetailId != cell.Id)) throw new QuestionException("VALIDATION_ERROR", "Câu hỏi đã được chọn ở ô khác.");
        var added = ids.Where(id => existing.All(x => x.QuestionId != id)).ToArray();
        if (existing.Count(x => x.MatrixDetailId == cell.Id) + added.Length > cell.QuestionCount) throw new QuestionException("QUESTION_QUOTA", "Số câu được chọn vượt yêu cầu ô ma trận.", 409);
        if (paper.ExamSetId is null)
        {
            var set = new ExamSet { ExamMatrixId = matrix.Id, Name = (task.Name + " · " + paper.Name)[..Math.Min(255, (task.Name + " · " + paper.Name).Length)], Status = "DRAFT", Purpose = "exam", CreatedByUserId = actor.UserId };
            db.ExamSets.Add(set); await db.SaveChangesAsync(ct); paper.ExamSetId = set.Id;
        }
        foreach (var id in added) db.ExamSetQuestions.Add(new() { ExamSetId = paper.ExamSetId.Value, MatrixDetailId = cell.Id, QuestionId = id });
        task.Status = "IN_PROGRESS"; task.QuestionVersion++; task.UpdatedAt = time.GetUtcNow().UtcDateTime; task.UpdatedByUserId = actor.UserId;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, paperId, ct);
    }

    public async Task<PaperQuestions> RemoveAsync(QuestionActor actor, ulong taskId, ulong paperId, ulong id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (task, paper, _) = await LoadAsync(actor, taskId, paperId, true, ct);
        if (task.QuestionVersion != version) throw new QuestionException("QUESTION_CONFLICT", "Nội dung đề đã thay đổi. Vui lòng tải lại.", 409);
        var row = await db.ExamSetQuestions.SingleOrDefaultAsync(x => x.ExamSetId == paper.ExamSetId && x.QuestionId == id, ct) ?? throw new QuestionException("QUESTION_NOT_FOUND", "Câu không nằm trong đề.", 404);
        db.ExamSetQuestions.Remove(row); task.QuestionVersion++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, paperId, ct);
    }
}
