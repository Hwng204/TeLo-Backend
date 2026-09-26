namespace Domain.Entities.Organization;

public sealed class Province
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DivisionType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset LastSyncedAt { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
