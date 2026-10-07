using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IEmailNotificationService
{
    Task<ServiceResult<EmailQueueResult>> QueueTestAsync(ulong schoolId, SendTestEmailRequest request, ulong actor, CancellationToken ct);
    Task QueueMatrixAsync(ulong matrixId, string eventCode, ulong actor, CancellationToken ct);
    Task QueueMatrixAssignmentAsync(ulong taskId, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailMessagePreview>> PreviewManualAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<EmailQueueResult>> QueueManualAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct);
    Task QueueEventAsync(ulong schoolId, ulong? branchId, string eventCode, string key, ulong actor, Dictionary<string, string> values, CancellationToken ct);
    Task<int> DeliverPendingAsync(CancellationToken ct);
}
