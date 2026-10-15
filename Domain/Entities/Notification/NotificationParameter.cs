namespace Domain.Entities.Notification;

public sealed class NotificationParameter
{
    public ulong Id { get; set; }
    public ulong NotificationConfigId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
