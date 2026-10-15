namespace Application.DTOs;

// Legacy dates are no longer stored; System.Text.Json ignores them if sent.
// Response/update Version is a computed state token, not a mapped DB column.
public sealed record CreateAcademicYearRequest(
    string Name,
    IReadOnlyList<ConfigureTermItem>? Terms = null);

public sealed record AcademicYearListItem(
    ulong Id,
    string Code,
    string Name,
    string Status,
    int SemesterCount)
{
    public uint Version => Application.Common.RevisionToken.For(Id, Name, Status);
}

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
    string Name);

public sealed record AcademicYearDetailDto(
    ulong Id,
    string Code,
    string Name,
    string Status,
    IReadOnlyList<SemesterDto> Semesters)
{
    public uint Version => Application.Common.RevisionToken.For(Id, Name, Status);
}

public sealed record UpdateAcademicYearRequest(
    string Name,
    IReadOnlyList<ConfigureTermItem>? Terms = null,
    uint? Version = null);

public sealed record ConfigureTermItem(
    byte Order,
    string Name);

public sealed record ConfigureTermsRequest(
    IReadOnlyList<ConfigureTermItem> Terms);
