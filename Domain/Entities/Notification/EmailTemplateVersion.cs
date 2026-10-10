namespace Domain.Entities.Notification;

public sealed class EmailTemplateVersion
{
    public ulong Id { get; set; }
    public ulong EmailTemplateId { get; set; }
    public uint Revision { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? VariablesJson { get; set; }
    public uint EventVersion { get; set; } = 1;
    public ulong CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public EmailTemplate Template { get; set; } = null!;
}
