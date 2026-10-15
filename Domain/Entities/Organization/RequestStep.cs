namespace Domain.Entities.Organization;

public sealed class RequestStep
{
    public ulong Id { get; set; }
    public ulong RequestProcessId { get; set; }
    public uint StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string HandlerRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
