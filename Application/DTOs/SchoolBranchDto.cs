namespace Application.DTOs;

public sealed record SchoolBranchDetailDto(
    ulong Id,
    ulong SchoolId,
    string Code,
    string Name,
    string? Address,
    string Status);

public sealed record CreateSchoolBranchRequest(
    string Name,
    string? Address,
    string? Status);

public sealed record UpdateSchoolBranchRequest(
    string Name,
    string? Address,
    string? Status);
