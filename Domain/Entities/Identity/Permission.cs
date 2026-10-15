namespace Domain.Entities.Identity;

public sealed class Permission
{
    public ulong RoleId { get; set; }
    public ulong NavbarId { get; set; }
    public ulong SchoolId { get; set; }
    // Column `permission`: the granted right (e.g. VIEW, EDIT).
    public string Value { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
    public Navbar Navbar { get; set; } = null!;
}
