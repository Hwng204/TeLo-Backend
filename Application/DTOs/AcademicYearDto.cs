namespace Application.DTOs;

public sealed record CreateAcademicYearRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record AcademicYearListItem(
    ulong Id,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    uint Version,
    int SemesterCount);

public sealed record AcademicYearPage(
    IReadOnlyList<AcademicYearListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record AcademicYearListQuery(
    string? Status,
    string? Search,
    int Page,
    int PageSize);

public sealed record SemesterDto(
    ulong Id,
    byte Order,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    uint Version);

public sealed record AcademicYearDetailDto(
    ulong Id,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    uint Version,
    IReadOnlyList<SemesterDto> Semesters);

public sealed record UpdateAcademicYearRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record ConfigureTermItem(
    byte Order,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record ConfigureTermsRequest(
    IReadOnlyList<ConfigureTermItem> Terms);
