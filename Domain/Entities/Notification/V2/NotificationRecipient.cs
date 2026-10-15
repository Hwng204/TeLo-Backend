namespace Domain.Entities.Notification;

// Bảng `notification_user` của ERD v2 (người nhận một thông báo).
public sealed class NotificationRecipient
{
    public ulong NotificationId { get; set; }
    public ulong UserId { get; set; }
    public DateTime? ReadAt { get; set; }
}
