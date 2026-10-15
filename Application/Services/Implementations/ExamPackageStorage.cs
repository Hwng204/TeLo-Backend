using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed record ExamStoredRow(ulong RowId, ulong DetailId, QuestionItem Snapshot);
public sealed record ExamStoredEvent(string Action, ulong ActorId, DateTime At, string? Comment = null, ulong? OldId = null, ulong? NewId = null);
public sealed record ExamPackageState(ulong CreatorId, DateTime CreatedAt, ulong? AcademicYearId, string AcademicYearName,
    string Purpose, ulong? SourceId, ulong? AuthoringTaskId, bool ReviewAssigned, ulong? FinalizedBy, DateTime? FinalizedAt,
    IReadOnlyList<ExamPackageCell> Cells, IReadOnlyList<ExamStoredRow> Rows, IReadOnlyList<ExamStoredEvent> History,
    bool Deleted = false, ulong? PracticeCopyId = null);

public sealed class ExamPackageStorage(ApplicationDbContext db, TaskJournal journal, TimeProvider time)
{
    public const string InternalTaskType = "EXAM_PACKAGE";
    public static string Stream(ulong setId) => "exam:" + setId;

    public async Task<(ExamPackageState State, int Version)> ReadAsync(ExamSet set, CancellationToken ct)
    {
        if (set.TaskId is { } taskId)
        {
            var entries = await journal.ReadLatestAsync(taskId, Stream(set.Id), ct);
            if (entries.LastOrDefault() is { } latest) return (latest.Read<ExamPackageState>(), entries.Count);
        }
        // Native sets created by the existing task module keep their stable IDs.
        var task = set.TaskId is { } id ? await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        var year = await db.AcademicYears.AsNoTracking().Where(x => x.Status == "ACTIVE").OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var rows = await QuestionBankService.Include(db.Questions.Where(q => db.ExamSetQuestions.Any(row => row.ExamSetId == set.Id && row.QuestionId == q.Id))).AsNoTracking().ToArrayAsync(ct);
        var nativeRows = await db.ExamSetQuestions.AsNoTracking().Where(x => x.ExamSetId == set.Id).OrderBy(x => x.Id).ToArrayAsync(ct);
        var matrix = await MatrixAsync(set.ExamMatrixId, ct);
        var snapshots = nativeRows.Select(row => new ExamStoredRow(row.Id,
            CellOf(matrix, rows.Single(q => q.Id == row.QuestionId)), QuestionService.Item(rows.Single(q => q.Id == row.QuestionId)))).ToArray();
        return (new(task?.AssignedToUserId ?? matrix.CreatedByUserId, task?.CreatedAt ?? time.GetUtcNow().UtcDateTime,
            year?.Id, year?.Name ?? "", "exam", null, task?.TaskType == "EXAM" ? task.Id : null,
            false, null, null, Cells(matrix), snapshots, []), 0);
    }

    public async Task<Dictionary<ulong, ExamPackageState>> ReadManyAsync(ExamSet[] sets, CancellationToken ct)
    {
        var rows = await journal.RowsAsync(sets.Where(set => set.TaskId.HasValue).Select(set => set.TaskId!.Value).Distinct().ToArray(),
            sets.Select(set => Stream(set.Id)).ToArray(), ct);
        var result = new Dictionary<ulong, ExamPackageState>();
        foreach (var set in sets)
        {
            var entries = TaskJournalCodec.Decode(rows.Where(row => row.TaskId == set.TaskId), Stream(set.Id), latestOnly: true);
            result[set.Id] = entries.LastOrDefault()?.Read<ExamPackageState>() ?? (await ReadAsync(set, ct)).State;
        }
        return result;
    }

    public static ExamPackageCell[] Cells(ExamMatrix matrix) => matrix.Details.OrderBy(x => x.Id).Select(x =>
        new ExamPackageCell(x.Id, x.Lesson.Title, x.CognitiveLevel, x.QuestionType, x.QuestionCount, x.Percentage,
            matrix.TotalScore * x.Percentage / 100)).ToArray();

    public static ulong CellOf(ExamMatrix matrix, Question question) => matrix.Details.SingleOrDefault(cell =>
        cell.LessonId == question.LessonId && cell.CognitiveLevel == question.QuestionTaskDetail.CognitiveLevel &&
        cell.QuestionType == question.QuestionTaskDetail.QuestionType)?.Id
        ?? throw new QuestionException("EXAM_STRUCTURE_INVALID", "Câu hỏi không khớp ô ma trận.", 409);

    public Task<ExamMatrix> MatrixAsync(ulong id, CancellationToken ct) => db.ExamMatrices.Include(x => x.Subject)
        .Include(x => x.GradeLevel).Include(x => x.Details).ThenInclude(x => x.Lesson).SingleAsync(x => x.Id == id, ct);

    public async Task WriteAsync(ExamSet set, ExamPackageState state, int version, string action, ulong actorId, CancellationToken ct,
        string? comment = null, ulong? oldId = null, ulong? newId = null)
    {
        if (set.TaskId is not { } taskId) throw new QuestionException("EXAM_STRUCTURE_INVALID", "Đề chưa có nhiệm vụ lưu lịch sử.", 409);
        var updated = state with { History = state.History.Append(new(action, actorId, time.GetUtcNow().UtcDateTime, comment, oldId, newId)).ToArray() };
        await journal.AppendAsync(taskId, Stream(set.Id), version, action, updated, actorId, ct,
            new(set.Id, state.Purpose, state.AcademicYearId));
    }

    public async Task SynchronizeAuthoringAsync(ExamPackageState state, ulong setId, CancellationToken ct)
    {
        if (state.AuthoringTaskId is not { } taskId) return;
        await db.LockTaskAsync(taskId, ct);
        var task = await db.WorkTasks.SingleAsync(x => x.Id == taskId, ct);
        // Native task_id moves to the dedicated review task. Persist a small
        // registry on the original authoring task so all papers remain visible.
        var entries = await journal.ReadAsync(taskId, "paper-links", ct);
        var ids = entries.LastOrDefault()?.Read<ulong[]>().ToHashSet() ?? [];
        ids.UnionWith(await db.ExamSets.Where(x => x.TaskId == taskId).Select(x => x.Id).ToArrayAsync(ct));
        ids.Add(setId);
        await journal.AppendAsync(taskId, "paper-links", entries.Count, "LINKS", ids.Order().ToArray(), task.CreatedByUserId, ct);
        var sets = await db.ExamSets.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        task.Status = sets.Length > 0 && sets.All(x => x.Status == "APPROVED") ? "COMPLETED"
            : sets.Length > 0 && sets.All(x => x.Status is "IN_REVIEW" or "REVIEWED" or "APPROVED") ? "PENDING_REVIEW" : "IN_PROGRESS";
        task.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }
}
