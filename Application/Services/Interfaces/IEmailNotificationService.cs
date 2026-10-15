namespace Application.Services.Interface;

// Khoá tạm DB v2: chỉ giữ các hàm ma trận/nhiệm vụ gọi; bản đầy đủ (gửi thử, gửi tay, hàng đợi) nằm trong
// EmailNotificationService.cs cũ, chờ chủ module email chuyển sang bảng notification* của ERD.
public interface IEmailNotificationService
{
    Task QueueMatrixAsync(ulong matrixId, string eventCode, ulong actor, CancellationToken ct);
    Task QueueMatrixAssignmentAsync(ulong taskId, ulong actor, CancellationToken ct);
    Task QueueEventAsync(ulong schoolId, ulong? branchId, string eventCode, string key, ulong actor, Dictionary<string, string> values, CancellationToken ct);
}
