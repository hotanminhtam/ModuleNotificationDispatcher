using ModuleNotificationDispatcher.Domain.Exceptions;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Providers;

/// <summary>
/// Simulates sending email notifications (500-1000ms delay, 20% failure rate).
/// </summary>
public class EmailNotificationProvider : INotificationProvider
{
    /// <inheritdoc />
    public NotificationType Type => NotificationType.Email;

    /// <inheritdoc />
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);

        if (Random.Shared.NextDouble() < 0.2)
            throw new NotificationDeliveryException("Email delivery failed.");
    }
}
