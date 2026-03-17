namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Represents a notification message to be dispatched.
/// </summary>
public class Notification
{
    /// <summary>
    /// Unique identifier for the notification.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Destination address (email address or phone number).
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// The message content to be sent.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Delivery priority (High, Medium, Low).
    /// </summary>
    public NotificationPriority Priority { get; set; }

    /// <summary>
    /// The channel used for delivery (Email, Sms).
    /// </summary>
    public NotificationType Type { get; set; }
}
