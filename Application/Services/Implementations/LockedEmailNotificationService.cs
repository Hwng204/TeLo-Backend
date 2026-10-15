using Application.Services.Interface;

namespace Application.Services.Implement;

// shortcut: khoá tạm DB v2 — không gửi email thông báo; thay bằng bản thật khi module email chuyển sang ERD.
public sealed class LockedEmailNotificationService : IEmailNotificationService
{
    public Task QueueMatrixAsync(ulong matrixId, string eventCode, ulong actor, CancellationToken ct) => Task.CompletedTask;

    public Task QueueMatrixAssignmentAsync(ulong taskId, ulong actor, CancellationToken ct) => Task.CompletedTask;

    public Task QueueEventAsync(ulong schoolId, ulong? branchId, string eventCode, string key, ulong actor,
        Dictionary<string, string> values, CancellationToken ct) => Task.CompletedTask;
}
