using Application.DTOs;

namespace Application.Services.Interface;

/// <summary>
/// Turns the user ids stored on matrices and tasks into names and role labels for display.
/// One batched lookup per request, so lists never trigger a query per row.
/// </summary>
public interface IMatrixPeopleResolver
{
    Task<IReadOnlyDictionary<ulong, MatrixPerson>> ResolveAsync(
        IEnumerable<ulong?> userIds,
        CancellationToken cancellationToken);
}
