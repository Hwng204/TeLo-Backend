namespace Domain.Entities.Organization;

public sealed class School
{
    public ulong Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    // v2 `school.province`: free text (the province catalog is locked until its module moves to v2).
    public string? ProvinceCode { get; set; }

    public ICollection<SchoolBranch> Branches { get; set; } = new List<SchoolBranch>();
}
