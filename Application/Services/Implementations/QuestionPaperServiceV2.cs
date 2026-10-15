using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Application.Services.Implement;

public sealed record PaperQuestionRequest([property: JsonRequired] int Version, ulong DetailId, IReadOnlyList<ulong>? QuestionIds);
public sealed record PaperQuestionCell(ulong Id, string LessonTitle, string CognitiveLevel, uint RequiredCount);
public sealed record SelectedPaperQuestion(ulong DetailId, QuestionItem Question);
public sealed record PaperQuestions(ulong TaskId, ulong PaperId, string Name, ulong MatrixId, int Version, bool CanEdit,
    IReadOnlyList<PaperQuestionCell> Cells, IReadOnlyList<SelectedPaperQuestion> Questions);

public sealed class QuestionPaperService(ApplicationDbContext db, TimeProvider time, ExamPackageStorage storage, QuestionAccess access)
{
    private async Task<(WorkTask Task, ExamSet Set, ExamMatrix Matrix, ExamPackageState State, int Version)> LoadAsync(
        QuestionActor actor, ulong taskId, ulong paperId, bool write, CancellationToken ct)
    {
        var user = await access.UserAsync(actor, ct);
        var set = await db.ExamSets.SingleOrDefaultAsync(x => x.Id == paperId, ct)
            ?? throw new QuestionException("PAPER_NOT_FOUND", "Không tìm thấy đề được giao.", 404);
        var matrix = await storage.MatrixAsync(set.ExamMatrixId, ct);
        if (user.SchoolBranchId != matrix.BranchId) throw new QuestionException("FORBIDDEN", "Đề không thuộc phân hiệu của bạn.", 403);
        if (write)
        {
            await db.SchoolBranches.FromSqlInterpolated($"SELECT * FROM branch WHERE branch_id = {matrix.BranchId} FOR UPDATE").ToArrayAsync(ct);
            await db.LockTaskAsync(taskId, ct);
            await db.ExamSets.FromSqlInterpolated($"SELECT * FROM exam_set WHERE exam_set_id = {paperId} FOR UPDATE").ToArrayAsync(ct);
            await db.Entry(set).ReloadAsync(ct);
            user = await access.UserAsync(actor, ct);
            if (user.SchoolBranchId != matrix.BranchId) throw new QuestionException("FORBIDDEN", "Phân hiệu đã thay đổi.", 403);
        }
        var task = await db.WorkTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.TaskType == "EXAM", ct);
        if (task is null || task.CreatedByUserId != actor.UserId && task.AssignedToUserId != actor.UserId)
            throw new QuestionException("FORBIDDEN", "Bạn không phụ trách nhiệm vụ tạo đề này.", 403);
        var (state, version) = await storage.ReadAsync(set, ct);
        if (set.TaskId != taskId && state.AuthoringTaskId != taskId)
            throw new QuestionException("PAPER_NOT_FOUND", "Đề không thuộc nhiệm vụ.", 404);
        if (write && (!actor.TeamLead || task.AssignedToUserId != actor.UserId || !QuestionRules.Editable(task.Status)))
            throw new QuestionException("FORBIDDEN", "Chỉ tổ trưởng được giao đang biên soạn mới được chọn câu hỏi.", 403);
        if (write && (state.ReviewAssigned || set.Status != "DRAFT"))
            throw new QuestionException("QUESTION_LOCKED", "Đề đang review hoặc đã khóa. Vui lòng mở chi tiết đề thi.", 409);
        return (task, set, matrix, state, version);
    }

    public async Task<PaperQuestions> GetAsync(QuestionActor actor, ulong taskId, ulong paperId, CancellationToken ct)
    {
        var (task, set, matrix, state, version) = await LoadAsync(actor, taskId, paperId, false, ct);
        return new(taskId, paperId, set.Name, matrix.Id, version,
            !state.ReviewAssigned && set.Status == "DRAFT" && actor.TeamLead && task.AssignedToUserId == actor.UserId && QuestionRules.Editable(task.Status),
            state.Cells.Select(x => new PaperQuestionCell(x.Id, x.LessonTitle, x.CognitiveLevel, x.RequiredCount)).ToArray(),
            state.Rows.Select(x => new SelectedPaperQuestion(x.DetailId, x.Snapshot)).ToArray());
    }

    public async Task<PaperQuestions> AddAsync(QuestionActor actor, ulong taskId, ulong paperId, PaperQuestionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var (task, set, matrix, state, version) = await LoadAsync(actor, taskId, paperId, true, ct);
        CheckVersion(version, request.Version);
        var ids = request.QuestionIds;
        if (ids is null || ids.Count is 0 or > 1000 || ids.Distinct().Count() != ids.Count)
            throw new QuestionException("VALIDATION_ERROR", "Danh sách câu được chọn không hợp lệ.");
        var cell = matrix.Details.SingleOrDefault(x => x.Id == request.DetailId)
            ?? throw new QuestionException("VALIDATION_ERROR", "Ô không thuộc ma trận.");
        var candidates = await QuestionBankService.Include(db.Questions.Where(x => ids.Contains(x.Id))).ToArrayAsync(ct);
        if (candidates.Length != ids.Count || candidates.Any(q => !ExamPackageRules.Matches(q, matrix, cell)))
            throw new QuestionException("VALIDATION_ERROR", "Có câu không còn phù hợp ô ma trận hoặc đã chuyển sang ôn tập.");
        var added = candidates.Where(q => state.Rows.All(row => row.Snapshot.Id != q.Id)).ToArray();
        if (state.Rows.Any(row => ids.Contains(row.Snapshot.Id) && row.DetailId != cell.Id) ||
            state.Rows.Count(row => row.DetailId == cell.Id) + added.Length > cell.QuestionCount)
            throw new QuestionException("QUESTION_QUOTA", "Số câu được chọn vượt yêu cầu ô ma trận.", 409);
        var rows = state.Rows.ToList();
        var nativeRows = added.Select(question => new ExamSetQuestion { ExamSetId = set.Id, QuestionId = question.Id }).ToArray();
        db.ExamSetQuestions.AddRange(nativeRows); await db.SaveChangesAsync(ct);
        rows.AddRange(nativeRows.Select((row, index) => new ExamStoredRow(row.Id, cell.Id, QuestionService.Item(added[index]))));
        await storage.WriteAsync(set, state with { Rows = rows, Deleted = false }, version, "PAPER_UPDATED", actor.UserId, ct);
        task.Status = "IN_PROGRESS"; task.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, paperId, ct);
    }

    public async Task<PaperQuestions> RemoveAsync(QuestionActor actor, ulong taskId, ulong paperId, ulong id, int expected, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var (task, set, _, state, version) = await LoadAsync(actor, taskId, paperId, true, ct);
        CheckVersion(version, expected);
        var row = await db.ExamSetQuestions.SingleOrDefaultAsync(x => x.ExamSetId == set.Id && x.QuestionId == id, ct)
            ?? throw new QuestionException("QUESTION_NOT_FOUND", "Câu không nằm trong đề.", 404);
        db.ExamSetQuestions.Remove(row);
        await storage.WriteAsync(set, state with { Rows = state.Rows.Where(x => x.Snapshot.Id != id).ToArray() }, version, "PAPER_UPDATED", actor.UserId, ct);
        task.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, taskId, paperId, ct);
    }

    private static void CheckVersion(int actual, int expected)
    {
        if (actual != expected || expected < 0) throw new QuestionException("QUESTION_CONFLICT", "Nội dung đề đã thay đổi. Vui lòng tải lại.", 409);
    }
}
