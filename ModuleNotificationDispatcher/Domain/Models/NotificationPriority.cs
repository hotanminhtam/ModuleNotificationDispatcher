namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Defines the delivery priority levels for notifications.
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// Urgent messages (e.g., OTP, Security Alerts).
    /// </summary>
    High = 1,

    /// <summary>
    /// Important business messages (e.g., Transactions, Receipts).
    /// </summary>
    Medium = 2,

    /// <summary>
    /// General non-urgent messages (e.g., Marketing, Newsletters).
    /// </summary>
    Low = 3,
}
