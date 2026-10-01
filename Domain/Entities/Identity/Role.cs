namespace Domain.Entities.Identity;

public sealed class Role
{
    public ulong Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public ulong? SchoolId { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public bool IsSystem { get; set; }
    public uint Version { get; set; } = 1;
    public DateTime? UsedAt { get; set; }
    public Domain.Entities.Organization.School? School { get; set; }
    public Domain.Entities.Organization.SchoolBranch? SchoolBranch { get; set; }

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
