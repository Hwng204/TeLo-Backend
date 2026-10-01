namespace Domain.Entities.Identity;

public sealed class Module
{
    public ulong Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public uint Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public ICollection<Navbar> Navbars { get; set; } = new List<Navbar>();
}
