using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Providers;

/// <summary>
/// Provider for sending SMS notifications.
/// </summary>
public class SmsNotificationProvider : INotificationProvider
{
    /// <inheritdoc />
    public NotificationType Type => NotificationType.Sms;

    /// <summary>
    /// Simulates sending an SMS notification with a random delay and failure rate.
    /// </summary>
    /// <param name="notification">The SMS notification to send.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation.</param>
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        // Simulate network latency (500ms - 1000ms)
        await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);

        // Simulate a 20% random failure rate for testing resilience
        if (Random.Shared.NextDouble() < 0.2)
        {
            throw new Exception("SMS delivery failed.");
        }
    }
}
