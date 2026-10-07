using Domain.Entities.Organization;

namespace Domain.Entities.Notification;

public sealed class NotificationConfig
{
    public ulong Id { get; set; }
    public ulong? SchoolId { get; set; }
    public ulong? SchoolBranchId { get; set; }
    public ulong? BaseConfigId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string Timing { get; set; } = string.Empty;
    public string RecipientScope { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public uint Version { get; set; } = 1;
    public ulong? EmailTemplateVersionId { get; set; }
    public EmailTemplateVersion? EmailTemplateVersion { get; set; }

    public School? School { get; set; }
    public SchoolBranch? SchoolBranch { get; set; }
    public NotificationConfig? BaseConfig { get; set; }
    public ICollection<NotificationConfig> DerivedConfigs { get; set; } = new List<NotificationConfig>();
    public ICollection<NotificationTarget> Targets { get; set; } = new List<NotificationTarget>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
