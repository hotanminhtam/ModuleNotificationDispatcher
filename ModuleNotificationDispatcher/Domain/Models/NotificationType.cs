namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// The delivery channel for a notification.
/// </summary>
public enum NotificationType
{
    /// <summary>Delivered via email.</summary>
    Email = 1,

    /// <summary>Delivered via SMS.</summary>
    Sms = 2
}
