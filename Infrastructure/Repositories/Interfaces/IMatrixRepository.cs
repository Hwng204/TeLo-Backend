using Domain.Entities.QuestionBank;
using Infrastructure.Models;

namespace Infrastructure.Repositories.Interface;

public interface IMatrixRepository : IGenericRepository<ExamMatrix>
{
    Task<PagedResult<MatrixListRow>> ListAsync(
        MatrixListFilter filter,
        CancellationToken cancellationToken);

    // Matrix with task, context and ordered details (each with lesson and chapter).
    Task<ExamMatrix?> GetAsync(ulong id, CancellationToken cancellationToken);

    Task<bool> ExistsForTaskAsync(ulong taskId, CancellationToken cancellationToken);

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
        ulong updatedByUserId,
        CancellationToken cancellationToken);
}
