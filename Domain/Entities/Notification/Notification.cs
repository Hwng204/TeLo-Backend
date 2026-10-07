using Domain.Entities.Organization;

namespace Domain.Entities.Notification;

public sealed class Notification
{
    public ulong Id { get; set; }
    public ulong? ConfigId { get; set; }
    public ulong SchoolId { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? EventCode { get; set; }
    public string? EventKey { get; set; }
    public ulong? EmailTemplateVersionId { get; set; }
    public EmailTemplateVersion? EmailTemplateVersion { get; set; }
    public bool IsTest { get; set; }
    public string SendKind { get; set; } = "AUTOMATIC";
    public string? RequestFingerprint { get; set; }
    public uint Version { get; set; } = 1;
    public DateTime? CancelledAt { get; set; }
    public ulong? CreatedByUserId { get; set; }

    public NotificationConfig? Config { get; set; }
    public School School { get; set; } = null!;
    public SchoolBranch? SchoolBranch { get; set; }
    public ICollection<NotificationRecipient> Recipients { get; set; } = new List<NotificationRecipient>();
}
