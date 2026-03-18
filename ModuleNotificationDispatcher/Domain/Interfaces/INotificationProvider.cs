using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Domain.Interfaces;

/// <summary>
/// Contract for a channel-specific notification sender.
/// </summary>
public interface INotificationProvider
{
    /// <summary>The channel this provider handles.</summary>
    NotificationType Type { get; }

    /// <summary>
    /// Sends a single notification through this channel.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
