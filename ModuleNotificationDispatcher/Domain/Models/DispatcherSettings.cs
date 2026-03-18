namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Configuration settings for the NotificationDispatcher, mapped from appsettings.json.
/// </summary>
public class DispatcherSettings
{
    /// <summary>Timeout per notification request, in seconds.</summary>
    public int PerRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum number of concurrent dispatches.</summary>
    public int MaxParallelism { get; set; } = 100;

    /// <summary>Maximum retry attempts on delivery failure.</summary>
    public int MaxRetry { get; set; } = 3;
}
