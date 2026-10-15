namespace Domain.Entities.Identity;

public sealed class Navbar
{
    public ulong Id { get; set; }
    public ulong ModuleId { get; set; }
    public ulong? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint DisplayOrder { get; set; }
    public string? UrlPath { get; set; }
    public string? Icon { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public Module Module { get; set; } = null!;
    public Navbar? Parent { get; set; }
    public ICollection<Navbar> Children { get; set; } = new List<Navbar>();
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
