using Domain.Entities.Examination;

namespace Infrastructure.Repositories.Interface;

public sealed record ExamListFilter(
    string? Keyword,
    ulong? SemesterId,
    ulong? SchoolBranchId,
    string? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber,
    int PageSize,
    string SortBy,
    string SortDirection);

public sealed record ExamListData(
    ulong Id,
    string Name,
    ulong SemesterId,
    string SemesterName,
    ulong SchoolBranchId,
    string SchoolBranchCode,
    string SchoolBranchName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public sealed record ExamDetailData(
    ulong Id,
    string Name,
    ulong SemesterId,
    string SemesterName,
    ulong SchoolBranchId,
    string SchoolBranchCode,
    string SchoolBranchName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int SubjectCount,
    int SessionCount,
    int RoomCount,
    int CandidateCount,
    int ProctorCount);

public sealed record ExamReferenceData(
    bool SemesterExists,
    bool SchoolBranchExists);

public interface IExamRepository
{
    Task<(IReadOnlyList<ExamListData> Items, int TotalCount)> ListAsync(
        ExamListFilter filter,
        CancellationToken cancellationToken);

    Task<ExamDetailData?> GetDetailAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task<Exam?> GetByIdAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task<Exam?> GetForDeleteAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task<ExamReferenceData> GetReferenceDataAsync(
        ulong semesterId,
        ulong schoolBranchId,
        CancellationToken cancellationToken);

    Task<bool> HasDependentDataAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task AddAsync(Exam exam, CancellationToken cancellationToken);

    void Delete(Exam exam);
}
