namespace Application.DTOs;

public sealed record CreateExamRequest(
    ulong SemesterId,
    ulong SchoolBranchId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record UpdateExamRequest(
    ulong SemesterId,
    ulong SchoolBranchId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public sealed record ExamSemesterSummary(
    ulong Id,
    string Name);

public sealed record ExamSchoolBranchSummary(
    ulong Id,
    string Code,
    string Name);

public sealed record ExamListItem(
    ulong Id,
    string Name,
    ExamSemesterSummary Semester,
    ExamSchoolBranchSummary SchoolBranch,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public sealed record ExamDetailDto(
    ulong Id,
    string Name,
    ExamSemesterSummary Semester,
    ExamSchoolBranchSummary SchoolBranch,
    DateOnly StartDate,
    DateOnly EndDate,
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
    string? SortDirection);
