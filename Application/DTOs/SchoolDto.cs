namespace Application.DTOs;

// ─── List ─────────────────────────────────────────────────────────────────────
public sealed record SchoolListItem(
    ulong Id,
    string Code,
    string Name,
    string Status,
    int BranchCount);

public sealed record SchoolListPage(
    IReadOnlyList<SchoolListItem> Items,
    int TotalCount);

// ─── Detail ───────────────────────────────────────────────────────────────────
public sealed record SchoolBranchItem(
    ulong Id,
    string Code,
    string Name,
    string? Address,
    string Status);

public sealed record SchoolDetailDto(
    ulong Id,
    string Code,
    string Name,
    string Status,
    string? ProvinceCode,
    IReadOnlyList<SchoolBranchItem> Branches);

// ─── Requests ─────────────────────────────────────────────────────────────────
public sealed record CreateSchoolRequest(
    string Name,
    string Code,
    string? Status,
    string? ProvinceCode);

public sealed record UpdateSchoolRequest(
    string Name,
    string? Status,
    string? ProvinceCode);
