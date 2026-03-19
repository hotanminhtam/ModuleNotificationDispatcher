using ModuleNotificationDispatcher.Domain.Exceptions;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Providers;

/// <summary>
/// Simulates sending SMS notifications (500-1000ms delay, 20% failure rate).
/// </summary>
public class SmsNotificationProvider : INotificationProvider
{
    /// <inheritdoc />
    public NotificationType Type => NotificationType.Sms;

    /// <inheritdoc />
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);

        if (Random.Shared.NextDouble() < 0.2)
            throw new NotificationDeliveryException("SMS delivery failed.");
    }
}
