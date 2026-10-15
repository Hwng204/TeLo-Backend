namespace Domain.Entities.Notification;

// Bảng `notification` của ERD v2.
public sealed class Notification
{
    public ulong Id { get; set; }
    public ulong NotificationConfigId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
