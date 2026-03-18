using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Domain.Interfaces;

public interface INotificationProvider
{
    NotificationType Type { get; }
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
