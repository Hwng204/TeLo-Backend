namespace Application.DTOs;

public sealed class IdentityListQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public ulong? SchoolId { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public ulong? RoleId { get; set; }
    public ulong? EligibleForRoleId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class SaveRoleRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public ulong? SchoolId { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public uint Version { get; set; }
}
public sealed class SaveModuleRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public uint Version { get; set; }
}
public sealed record IdentityStatusRequest(string Status, uint Version);
public sealed record AssignRoleUsersRequest(ulong[] UserIds, uint Version);
public sealed record AssignUserRolesRequest(ulong[] RoleIds, uint Version);
public sealed record RoleItem(ulong Id, string Code, string Name, string? Description, string Status,
    ulong? SchoolId, string? SchoolName, ulong? SchoolBranchId, string? SchoolBranchName,
    bool IsSystem, uint Version, int UserCount, bool CanDelete);
public sealed record ModuleItem(ulong Id, string Code, string Name, string? Description,
    string Status, uint Version, int NavbarCount, bool CanDelete);
public sealed record IdentityUserItem(ulong Id, string Username, string FullName, string Email, string Status,
    ulong? SchoolId, string? SchoolName, ulong? SchoolBranchId, string? SchoolBranchName, uint Version);
public sealed record UserRolesDetail(IdentityUserItem User, IReadOnlyList<RoleItem> Roles);
public sealed record IdentityScopeItem(ulong Id, string Code, string Name, string Status);

// ===== User management (Admin) =====
public sealed class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Password { get; set; } = "";
    public string? MoetIdentifier { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public ulong[] RoleIds { get; set; } = [];
}
public sealed class UpdateUserRequest
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? MoetIdentifier { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public uint Version { get; set; }
}
public sealed record ResetUserPasswordRequest(string NewPassword, uint Version);
public sealed record UserDetailItem(ulong Id, string Username, string FullName, string Email, string? MoetIdentifier,
    string Status, ulong? SchoolId, string? SchoolName, ulong? SchoolBranchId, string? SchoolBranchName,
    DateTime CreatedAt, bool IsTeacher, bool IsStudent, uint Version, IReadOnlyList<RoleItem> Roles);
