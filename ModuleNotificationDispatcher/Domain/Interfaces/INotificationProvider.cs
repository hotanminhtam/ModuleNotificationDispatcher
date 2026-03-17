using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Domain.Interfaces;

/// <summary>
/// Defines the contract for notification delivery services.
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Gets the type of notification this provider handles.
    /// </summary>
    NotificationType Type { get; }

    /// <summary>
    /// Sends the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification data to send.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
