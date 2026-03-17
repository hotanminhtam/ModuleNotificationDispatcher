using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Domain.Interfaces;

namespace ModuleNotificationDispatcher.Services;

/// <summary>
/// Provider for sending email notifications.
/// </summary>
public class EmailNotificationProvider : INotificationProvider
{
    /// <inheritdoc />
    public NotificationType Type => NotificationType.Email;

    /// <summary>
    /// Simulates sending an email notification with a random delay and failure rate.
    /// </summary>
    /// <param name="notification">The email notification to send.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation.</param>
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        // Simulate network latency (500ms - 1000ms)
        await Task.Delay(Random.Shared.Next(500, 1000), cancellationToken);

        // Simulate a 20% random failure rate for testing resilience
        if (Random.Shared.NextDouble() < 0.2)
        {
            throw new Exception("Email delivery failed.");
        }
    }
}
