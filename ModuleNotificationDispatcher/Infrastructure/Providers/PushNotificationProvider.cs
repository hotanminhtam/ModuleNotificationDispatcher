using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Providers;

/// <summary>
/// Provider for sending Mobile Push notifications.
/// </summary>
public class PushNotificationProvider : INotificationProvider
{
    /// <inheritdoc />
    public NotificationType Type => NotificationType.Push;

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        // Simulate high-speed push notification delivery (100ms - 300ms)
        await Task.Delay(Random.Shared.Next(100, 300), cancellationToken);

        // Simulate a 5% failure rate
        if (Random.Shared.NextDouble() < 0.05)
        {
            throw new Exception("Push notification delivery failed.");
        }
    }
}
