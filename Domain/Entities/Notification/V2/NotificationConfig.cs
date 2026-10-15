namespace Domain.Entities.Notification;

// Bảng `notification_config` của ERD v2; bản cũ (phạm vi trường/phân hiệu, lịch gửi) chờ module email viết lại.
public sealed class NotificationConfig
{
    public ulong Id { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
