namespace Domain.Entities.Examination;

public sealed class IncidentType
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
