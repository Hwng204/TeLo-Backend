namespace Domain.Entities.Organization;

public sealed class RequestHistory
{
    public ulong Id { get; set; }
    public ulong RequestId { get; set; }
    public ulong RequestStepId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public ulong HandleBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
