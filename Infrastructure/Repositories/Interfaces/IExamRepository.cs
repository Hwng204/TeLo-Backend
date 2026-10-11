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
    string SortDirection,
    ulong? AcademicYearId);

public sealed record ExamListData(
    ulong Id,
    string Name,
    ulong SemesterId,
    string SemesterName,
    ulong AcademicYearId,
    string AcademicYearName,
    ulong? GradeLevelId,
    string? GradeLevelName,
    ulong? SubjectId,
    string? SubjectName,
    ulong SchoolBranchId,
    string SchoolBranchCode,
    string SchoolBranchName,
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly EndDate,
    TimeOnly EndTime,
    string Status);

public sealed record ExamDetailData(
    ulong Id,
    string Name,
    ulong SemesterId,
    string SemesterName,
    ulong AcademicYearId,
    string AcademicYearName,
    ulong? GradeLevelId,
    string? GradeLevelName,
    ulong? SubjectId,
    string? SubjectName,
    ulong SchoolBranchId,
    string SchoolBranchCode,
    string SchoolBranchName,
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly EndDate,
    TimeOnly EndTime,
    string Status,
    int SubjectCount,
    int SessionCount,
    int RoomCount,
    int CandidateCount,
    int ProctorCount);

public sealed record ExamReferenceData(
    bool SemesterExists,
    bool SchoolBranchExists,
    bool GradeLevelExists = true,
    bool SubjectExists = true);

public sealed record ExamOptionData(ulong Id, string Name);
public sealed record ExamSemesterOptionData(ulong Id, string Name);
public sealed record ExamAcademicYearOptionData(
    ulong Id,
    string Name,
    string Status,
    IReadOnlyList<ExamSemesterOptionData> Semesters);
public sealed record ExamFormOptionsData(
    IReadOnlyList<ExamAcademicYearOptionData> AcademicYears,
    IReadOnlyList<ExamOptionData> GradeLevels,
    IReadOnlyList<ExamOptionData> Subjects);

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
        ulong gradeLevelId,
        ulong subjectId,
        CancellationToken cancellationToken);

    Task<ExamFormOptionsData> GetFormOptionsAsync(CancellationToken cancellationToken);

    Task<int> CountCandidatesAsync(
        ulong academicYearId,
        ulong schoolBranchId,
        ulong gradeLevelId,
        CancellationToken cancellationToken);

    Task<bool> HasDependentDataAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task AddAsync(Exam exam, CancellationToken cancellationToken);

    void Delete(Exam exam);
}
