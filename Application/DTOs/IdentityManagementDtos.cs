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

// Roles are global: description, schoolId, schoolBranchId and version in the JSON body are ignored.
public sealed class SaveRoleRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class SaveModuleRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}
// Version is the user's security_version (users only); role and module status ignore it.
public sealed record IdentityStatusRequest(string Status, uint Version);
public sealed record AssignRoleUsersRequest(ulong[] UserIds);
public sealed record AssignUserRolesRequest(ulong[] RoleIds, uint Version);
public sealed record RoleItem(ulong Id, string Code, string Name, string Status,
    bool IsSystem, int UserCount, bool CanDelete, string? DeleteBlockedReason = null);
public sealed record ModuleItem(ulong Id, string Code, string Name, string? Description,
    string Status, int NavbarCount, bool CanDelete);
public sealed record IdentityUserItem(ulong Id, string Username, string FullName, string Email, string Status,
    ulong? SchoolId, string? SchoolName, ulong? SchoolBranchId, string? SchoolBranchName, uint Version);
public sealed record UserRolesDetail(IdentityUserItem User, IReadOnlyList<RoleItem> Roles);
public sealed record IdentityScopeItem(ulong Id, string Code, string Name, string Status);

// ===== User management (Admin) =====
// No user.email column: `email` in requests is ignored; responses derive it from teacher.email ("" when none).
public sealed class CreateUserRequest
{
    public string Username { get; set; } = "";
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
    public string FullName { get; set; } = "";
    public string? MoetIdentifier { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public string? NewPassword { get; set; }
    public uint Version { get; set; }
}
public sealed record ResetUserPasswordRequest(string NewPassword, uint Version);
public sealed record UserDetailItem(ulong Id, string Username, string FullName, string Email, string? MoetIdentifier,
    string Status, ulong? SchoolId, string? SchoolName, ulong? SchoolBranchId, string? SchoolBranchName,
    DateTime CreatedAt, bool IsTeacher, bool IsStudent, uint Version, IReadOnlyList<RoleItem> Roles);
