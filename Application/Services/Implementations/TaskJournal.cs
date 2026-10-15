using Application.Common;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed class TaskJournal(ApplicationDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<TaskJournalCodec.Entry>> ReadAsync(ulong taskId, string stream, CancellationToken ct) =>
        TaskJournalCodec.Decode(await db.TaskHistories.AsNoTracking().Where(x => x.TaskId == taskId).OrderBy(x => x.Id).ToArrayAsync(ct), stream);

    public async Task<IReadOnlyList<TaskJournalCodec.Entry>> ReadLatestAsync(ulong taskId, string stream, CancellationToken ct) =>
        TaskJournalCodec.Decode(await RowsAsync([taskId], [stream], ct), stream, latestOnly: true);

    public Task<TaskHistory[]> RowsAsync(ulong[] taskIds, string[] streams, CancellationToken ct)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TaskHistory), "row");
        System.Linq.Expressions.Expression body = System.Linq.Expressions.Expression.Constant(false);
        foreach (var stream in streams.Distinct())
        {
            var contains = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Property(parameter, nameof(TaskHistory.Content)),
                nameof(string.Contains), Type.EmptyTypes, System.Linq.Expressions.Expression.Constant("\"stream\":\"" + stream + "\""));
            body = System.Linq.Expressions.Expression.OrElse(body, contains);
        }
        var filter = System.Linq.Expressions.Expression.Lambda<Func<TaskHistory, bool>>(body, parameter);
        return db.TaskHistories.AsNoTracking().Where(row => taskIds.Contains(row.TaskId) && row.Content.StartsWith(TaskJournalCodec.Prefix))
            .Where(filter).OrderBy(row => row.Id).ToArrayAsync(ct);
    }

    // Caller holds the task row lock and an outer transaction. Chunks and native
    // question/set/task mutations commit or roll back as one operation.
    public async Task AppendAsync<T>(ulong taskId, string stream, int version, string action, T data, ulong actorId, CancellationToken ct,
        TaskJournalCodec.Lookup? lookup = null)
    {
        var current = await ReadLatestAsync(taskId, stream, ct);
        if (current.Count != version) throw new QuestionException("QUESTION_CONFLICT", "Dữ liệu đã thay đổi. Vui lòng tải lại.", 409);
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var content in TaskJournalCodec.Encode(stream, version + 1, action, data, lookup))
            db.TaskHistories.Add(new() { TaskId = taskId, Content = content, CreateBy = actorId, CreateAt = now });
        await db.SaveChangesAsync(ct);
    }
}
