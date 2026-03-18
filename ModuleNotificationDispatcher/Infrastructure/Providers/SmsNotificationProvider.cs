using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Providers;

/// <summary>
/// Simulates sending SMS notifications (500-1000ms delay, 20% failure rate).
/// </summary>
public class SmsNotificationProvider : INotificationProvider
{
    public NotificationType Type => NotificationType.Sms;

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);

        if (Random.Shared.NextDouble() < 0.2)
            throw new Exception("SMS delivery failed.");
    }
}
