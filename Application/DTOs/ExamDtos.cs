namespace Application.DTOs;

public sealed record CreateExamRequest(
    ulong SemesterId,
    ulong SchoolBranchId,
    ulong GradeLevelId,
    ulong SubjectId,
    string Name,
    DateOnly? StartDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    TimeOnly? EndTime);

public sealed record UpdateExamRequest(
    ulong SemesterId,
    ulong SchoolBranchId,
    ulong GradeLevelId,
    ulong SubjectId,
    string Name,
    DateOnly? StartDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    TimeOnly? EndTime,
    string Status);

public sealed record ExamSemesterSummary(
    ulong Id,
    string Name);

public sealed record ExamAcademicYearSummary(
    ulong Id,
    string Name);

public sealed record ExamCatalogSummary(
    ulong Id,
    string Name);

public sealed record ExamSemesterOption(
    ulong Id,
    string Name);

public sealed record ExamAcademicYearOption(
    ulong Id,
    string Name,
    string Status,
    IReadOnlyList<ExamSemesterOption> Semesters);

public sealed record ExamFormOptionsDto(
    IReadOnlyList<ExamAcademicYearOption> AcademicYears,
    IReadOnlyList<ExamCatalogSummary> GradeLevels,
    IReadOnlyList<ExamCatalogSummary> Subjects);

public sealed record ExamCandidateCountDto(
    ulong AcademicYearId,
    ulong SchoolBranchId,
    ulong GradeLevelId,
    int StudentCount);

public sealed record ExamSchoolBranchSummary(
    ulong Id,
    string Code,
    string Name);

public sealed record ExamListItem(
    ulong Id,
    string Name,
    ExamSemesterSummary Semester,
    ExamAcademicYearSummary AcademicYear,
    ExamCatalogSummary? GradeLevel,
    ExamCatalogSummary? Subject,
    ExamSchoolBranchSummary SchoolBranch,
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly EndDate,
    TimeOnly EndTime,
    string Status);

public sealed record ExamDetailDto(
    ulong Id,
    string Name,
    ExamSemesterSummary Semester,
    ExamAcademicYearSummary AcademicYear,
    ExamCatalogSummary? GradeLevel,
    ExamCatalogSummary? Subject,
    ExamSchoolBranchSummary SchoolBranch,
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

public sealed record ExamPage(
    IReadOnlyList<ExamListItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record ExamListQuery(
    string? Keyword,
    ulong? SemesterId,
    ulong? SchoolBranchId,
    string? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber,
    int PageSize,
    string? SortBy,
    string? SortDirection,
    ulong? AcademicYearId = null);
