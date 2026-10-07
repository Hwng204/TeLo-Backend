using Domain.Entities.Identity;

namespace Domain.Entities.Notification;

public sealed class NotificationRecipient
{
    public ulong Id { get; set; }
    public ulong NotificationId { get; set; }
    public ulong UserId { get; set; }
    public string EmailStatus { get; set; } = "PENDING";
    public DateTime? EmailSentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? EmailAddress { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? DeliveryToken { get; set; }
    public string? LastError { get; set; }

    public Notification Notification { get; set; } = null!;
    public User User { get; set; } = null!;
}
