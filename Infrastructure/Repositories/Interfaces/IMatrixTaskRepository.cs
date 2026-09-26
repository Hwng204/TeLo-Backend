using Domain.Entities.QuestionBank;
using Infrastructure.Models;

namespace Infrastructure.Repositories.Interface;

public interface IMatrixTaskRepository : IGenericRepository<WorkTask>
{
    Task<PagedResult<MatrixTaskRow>> ListAsync(
        MatrixTaskFilter filter,
        CancellationToken cancellationToken);

    // Any task type; used when a matrix is created from a task.
    Task<WorkTask?> GetAsync(ulong taskId, CancellationToken cancellationToken);

    // Only tasks of type MATRIX.
    Task<WorkTask?> GetMatrixTaskAsync(ulong taskId, CancellationToken cancellationToken);

    Task<ulong?> GetLinkedMatrixIdAsync(ulong taskId, CancellationToken cancellationToken);

    Task<ulong?> GetContextBranchIdAsync(ulong academicContextId, CancellationToken cancellationToken);

    // Deletes a MATRIX task only while no matrix exists for it; false when nothing was deleted.
    Task<bool> DeleteIfNotStartedAsync(ulong taskId, CancellationToken cancellationToken);
}
