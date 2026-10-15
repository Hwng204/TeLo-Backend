namespace Domain.Entities.Identity;

public sealed class Role
{
    public ulong Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
