namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Defines the communication channels available for notifications.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Delivery via Email service.
    /// </summary>
    Email = 1,

    /// <summary>
    /// Delivery via SMS gateway.
    /// </summary>
    Sms = 2,

    /// <summary>
    /// Delivery via Mobile Push service (Firebase/APNs).
    /// </summary>
    Push = 3
}
