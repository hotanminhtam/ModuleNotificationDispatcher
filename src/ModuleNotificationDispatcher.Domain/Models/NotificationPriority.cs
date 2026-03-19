namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Priority level that determines dispatch ordering (lower value = higher priority).
/// </summary>
public enum NotificationPriority
{
    /// <summary>Dispatched first.</summary>
    High = 1,

    /// <summary>Dispatched after High.</summary>
    Medium = 2,

    /// <summary>Dispatched last.</summary>
    Low = 3
}
