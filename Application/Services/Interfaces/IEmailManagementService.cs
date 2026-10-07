using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IEmailManagementService
{
    Task<ServiceResult<DirectoryPage<EmailSchoolItem>>> SchoolsAsync(EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailEventItem>>> EventsAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailEventItem>> EventAsync(ulong schoolId, string code, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailEventItem>> SaveEventAsync(ulong schoolId, string? code, SaveEmailEventRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailEventItem>> EventStatusAsync(ulong schoolId, string code, EmailStatusRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteEventAsync(ulong schoolId, string code, uint version, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailRuntimeStatus>> RuntimeAsync(ulong schoolId, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailMessagePreview>> MessagePreviewAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailQueueResult>> SendAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> CancelAsync(ulong schoolId, ulong id, uint version, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailTemplateItem>>> TemplatesAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailTemplateDetail>> TemplateAsync(ulong schoolId, ulong id, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailTemplateDetail>> SaveTemplateAsync(ulong schoolId, ulong? id, SaveEmailTemplateRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailRevisionItem>>> RevisionsAsync(ulong schoolId, ulong id, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailTemplateDetail>> TemplateStatusAsync(ulong schoolId, ulong id, EmailStatusRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteTemplateAsync(ulong schoolId, ulong id, uint version, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailConfigurationListItem>>> ConfigurationsAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailConfigItem>> ConfigurationAsync(ulong schoolId, string eventCode, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailConfigItem>> SaveConfigurationAsync(ulong schoolId, string eventCode, SaveEmailConfigRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailRecipientOption>>> RecipientsAsync(ulong schoolId, string kind, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailRecipientOption>>> PreviewAsync(ulong schoolId, SaveEmailConfigRequest request, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailHistoryItem>>> HistoryAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailHistoryDetail>> HistoryDetailAsync(ulong schoolId, ulong id, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<EmailDeliveryItem>>> DeliveriesAsync(ulong schoolId, ulong id, EmailListQuery query, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailQueueResult>> TestAsync(ulong schoolId, SendTestEmailRequest request, ulong actor, CancellationToken ct);
}
