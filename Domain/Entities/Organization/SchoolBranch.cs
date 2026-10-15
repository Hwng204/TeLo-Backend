namespace Domain.Entities.Organization;

public sealed class SchoolBranch
{
    public ulong Id { get; set; }
    public ulong SchoolId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Logo { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public School School { get; set; } = null!;
    public ICollection<SchoolClass> Classes { get; set; } = new List<SchoolClass>();
}
