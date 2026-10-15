namespace Domain.Entities.Organization;

public sealed class RequestAttachment
{
    public ulong Id { get; set; }
    public ulong RequestId { get; set; }
    public ulong RequestStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ulong UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}
