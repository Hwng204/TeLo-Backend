namespace Application.DTOs;

public sealed class EmailListQuery
{
    public string? Search { get; set; }
    public string? EventCode { get; set; }
    public string? Status { get; set; }
    public string? SendKind { get; set; }
    public string? ConfigurationState { get; set; }
    public string? TriggerKind { get; set; }
    public ulong? BranchId { get; set; }
    public ulong? SchoolId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record EmailSchoolItem(ulong Id, string Code, string Name, bool CanManageTemplates);
public sealed record EmailEventVariable(string Name, string Label, string Type = "TEXT", bool Required = true);
public sealed record EmailEventItem(string Code, string Name, IReadOnlyList<string> Variables,
    ulong Id = 0, string TriggerKind = "SYSTEM", string Status = "ACTIVE", uint Version = 1,
    string Description = "", IReadOnlyList<EmailEventVariable>? VariableDefinitions = null, bool CanDelete = false);
public sealed class SaveEmailEventRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public uint Version { get; set; }
    public EmailEventVariable[] VariableDefinitions { get; set; } = [];
}
public sealed record EmailRuntimeStatus(bool Enabled, bool SmtpConfigured);
public sealed record EmailTemplateItem(ulong Id, string Code, string Name, string EventCode, string Status,
    uint Version, ulong LatestRevisionId, bool CanDelete);
public sealed record EmailRevisionItem(ulong Id, uint Revision, string Subject, string Body, DateTime CreatedAt,
    string? VariablesJson = null, uint EventVersion = 1);
public sealed record EmailTemplateDetail(EmailTemplateItem Template, EmailRevisionItem CurrentRevision);
public sealed class SaveEmailTemplateRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string EventCode { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public uint Version { get; set; }
}
public sealed record EmailStatusRequest(string Status, uint Version);
public sealed record EmailTargetItem(ulong? RoleId, ulong? UserId, string Action, string? Name = null);
public sealed class SaveEmailConfigRequest
{
    public ulong EmailTemplateVersionId { get; set; }
    public uint Version { get; set; }
    public bool IsActive { get; set; }
    public string RecipientScope { get; set; } = "NONE";
    public EmailTargetItem[] Targets { get; set; } = [];
}
public sealed record EmailConfigItem(ulong? Id, string EventCode, ulong? EmailTemplateVersionId,
    uint Version, bool IsActive, string RecipientScope, IReadOnlyList<EmailTargetItem> Targets,
    string? TemplateName = null, uint? Revision = null, string? TemplateStatus = null,
    IReadOnlyList<EmailEventVariable>? VariableDefinitions = null);
public sealed record EmailRecipientOption(ulong Id, string Name, string? Email, string? BranchName, string? Code);
public sealed record EmailHistoryItem(ulong Id, string Title, string? EventCode, DateTime CreatedAt,
    int RecipientCount, int SentCount, int PendingCount, int ErrorCount, int CancelledCount, bool IsTest,
    DateTime? ScheduledFor = null, string SendKind = "AUTOMATIC", uint Version = 1, bool CanCancel = false);
public sealed record EmailDeliveryItem(ulong Id, ulong UserId, string Name, string? Email, string Status,
    int Attempts, string? Error, DateTime? SentAt);
public sealed record EmailHistoryDetail(EmailHistoryItem Notification, string Content, string? ActionUrl);
public sealed record SendTestEmailRequest(ulong RevisionId, ulong RecipientUserId, Guid RequestId);
public sealed record EmailQueueResult(ulong NotificationId, string Status);
public sealed class SendEmailRequest
{
    public string EventCode { get; set; } = "";
    public uint ConfigVersion { get; set; }
    public Guid RequestId { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? PreviewFingerprint { get; set; }
}
public sealed record EmailMessagePreview(string Subject, string Body, int RecipientCount, string Fingerprint);
public sealed record CancelEmailRequest(uint Version);

public sealed record EmailConfigurationListItem(string EventCode, string EventName, string TriggerKind,
    string EventStatus, ulong? ConfigId, uint Version, string ConfigurationState, string? TemplateName,
    uint? Revision, string? TemplateStatus, int TargetCount);

