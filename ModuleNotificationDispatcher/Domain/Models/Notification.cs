namespace ModuleNotificationDispatcher.Domain.Models;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Destination { get; set; }
    public string? Message { get; set; }
    public NotificationPriority Priority { get; set; }
    public NotificationType Type { get; set; }
}
