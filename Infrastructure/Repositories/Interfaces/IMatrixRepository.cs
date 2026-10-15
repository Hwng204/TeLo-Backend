using Domain.Entities.QuestionBank;
using Infrastructure.Models;

namespace Infrastructure.Repositories.Interface;

public interface IMatrixRepository : IGenericRepository<ExamMatrix>
{
    Task<PagedResult<MatrixListRow>> ListAsync(
        MatrixListFilter filter,
        CancellationToken cancellationToken);

    // Matrix with task and ordered details (each with lesson and chapter).
    Task<ExamMatrix?> GetAsync(ulong id, CancellationToken cancellationToken);

    Task<bool> TryUpdateStatusAsync(
        ExamMatrix matrix,
        string expectedStatus,
        CancellationToken cancellationToken);

    Task<bool> LockWithStatusAsync(
        ulong matrixId,
        string expectedStatus,
        CancellationToken cancellationToken);

    // Names and role codes for a set of users, keyed by user id. Unknown ids are simply absent.
    Task<IReadOnlyDictionary<ulong, MatrixPersonRow>> GetPeopleAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken);

    Task SetTaskStatusAsync(
        ulong taskId,
        string status,
        CancellationToken cancellationToken);

    // Tracked for the next CompleteAsync.
    void AddTaskHistory(TaskHistory history);

    // Newest task_history row of the task (the last rejection), or null.
    Task<TaskHistory?> GetLatestTaskHistoryAsync(ulong taskId, CancellationToken cancellationToken);
}
