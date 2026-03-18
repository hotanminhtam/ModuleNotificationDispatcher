namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Represents a notification to be dispatched through a specific channel.
/// </summary>
public class Notification
{
    /// <summary>Unique identifier for this notification.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Recipient address (email or phone number).</summary>
    public string? Destination { get; set; }

    /// <summary>Content of the notification.</summary>
    public string? Message { get; set; }

    /// <summary>Dispatch priority (High, Medium, Low).</summary>
    public NotificationPriority Priority { get; set; }

    /// <summary>Channel type (Email, SMS).</summary>
    public NotificationType Type { get; set; }
}
