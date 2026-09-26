using Domain.Entities.Academic;

namespace Infrastructure.Repositories.Interface;

public enum AcademicYearCreateOutcome
{
    Created,
    Conflict
}

public sealed record AcademicYearListFilter(
    string? Status,
    string? Search,
    int Page,
    int PageSize);

public interface IAcademicYearRepository
{
    Task<AcademicYearCreateOutcome> TryAddAsync(
        AcademicYear academicYear,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<AcademicYear> Items, int TotalCount)> ListAsync(
        AcademicYearListFilter filter,
        CancellationToken cancellationToken);

    Task<AcademicYear?> GetByIdWithSemestersAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task<bool> HasConflictExceptCurrentAsync(
        ulong currentYearId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);

    Task<bool> HasActiveYearAsync(
        ulong exceptYearId,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        AcademicYear academicYear,
        CancellationToken cancellationToken);
}
