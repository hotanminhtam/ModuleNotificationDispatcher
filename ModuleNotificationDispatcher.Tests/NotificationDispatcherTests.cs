using Moq;
using ModuleNotificationDispatcher.Application;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Resilience;
using Xunit;

namespace ModuleNotificationDispatcher.Tests;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldProcessAllNotifications()
    {
        // Arrange
        var mockEmailProvider = new Mock<INotificationProvider>();
        mockEmailProvider.Setup(p => p.Type).Returns(NotificationType.Email);
        mockEmailProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

        var mockSmsProvider = new Mock<INotificationProvider>();
        mockSmsProvider.Setup(p => p.Type).Returns(NotificationType.Sms);
        mockSmsProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

        var providers = new List<INotificationProvider> { mockEmailProvider.Object, mockSmsProvider.Object };
        var dispatcher = new NotificationDispatcher(providers, TimeSpan.FromSeconds(5), 10, 3);

        var notifications = new List<Notification>
        {
            new Notification { Type = NotificationType.Email, Priority = NotificationPriority.High },
            new Notification { Type = NotificationType.Sms, Priority = NotificationPriority.Medium }
        };

        // Act
        await dispatcher.DispatchAsync(notifications, CancellationToken.None);

        // Assert
        mockEmailProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        mockSmsProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleRetryOnFailure()
    {
        // Arrange
        var mockEmailProvider = new Mock<INotificationProvider>();
        mockEmailProvider.Setup(p => p.Type).Returns(NotificationType.Email);
        
        int callCount = 0;
        mockEmailProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                         .Returns(() => {
                             callCount++;
                             if (callCount <= 2) throw new Exception("Temporary failure");
                             return Task.CompletedTask;
                         });

        var providers = new List<INotificationProvider> { mockEmailProvider.Object };
        var dispatcher = new NotificationDispatcher(providers, TimeSpan.FromSeconds(5), 10, 3);

        var notifications = new List<Notification> { new Notification { Type = NotificationType.Email } };

        // Act
        await dispatcher.DispatchAsync(notifications, CancellationToken.None);

        // Assert
        mockEmailProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task DispatchAsync_ShouldLogFailureWhenRetriesExhausted()
    {
        // Arrange
        var mockEmailProvider = new Mock<INotificationProvider>();
        mockEmailProvider.Setup(p => p.Type).Returns(NotificationType.Email);
        mockEmailProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new Exception("Permanent failure"));

        var providers = new List<INotificationProvider> { mockEmailProvider.Object };
        var dispatcher = new NotificationDispatcher(providers, TimeSpan.FromSeconds(5), 1, 2);

        var notifications = new List<Notification> { new Notification { Type = NotificationType.Email } };

        // Act
        await dispatcher.DispatchAsync(notifications, CancellationToken.None);

        // Assert
        // Initial + 2 retries = 3 calls
        mockEmailProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Retry_ExecuteAsync_ShouldThrowOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await Retry.ExecuteAsync(() => Task.CompletedTask, cts.Token, 3));
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleTimeout()
    {
        // Arrange
        var mockEmailProvider = new Mock<INotificationProvider>();
        mockEmailProvider.Setup(p => p.Type).Returns(NotificationType.Email);
        mockEmailProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                         .Returns(async (Notification n, CancellationToken ct) => {
                             await Task.Delay(2000, ct); // Delay longer than timeout
                         });

        var providers = new List<INotificationProvider> { mockEmailProvider.Object };
        // Set perRequestTimeout to 500ms
        var dispatcher = new NotificationDispatcher(providers, TimeSpan.FromMilliseconds(500), 1, 0);

        var notifications = new List<Notification> { new Notification { Type = NotificationType.Email } };

        // Act
        await dispatcher.DispatchAsync(notifications, CancellationToken.None);

        // Assert
        mockEmailProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
