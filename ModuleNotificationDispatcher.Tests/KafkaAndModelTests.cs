using Moq;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Interfaces;
using Xunit;

namespace ModuleNotificationDispatcher.Tests;

public class ModelTests
{
    [Fact]
    public void Notification_ShouldInitializeWithDefaultValues()
    {
        // Act
        var notification = new Notification();

        // Assert
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Null(notification.Destination);
        Assert.Null(notification.Message);
    }

    [Fact]
    public void Notification_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        
        // Act
        var notification = new Notification
        {
            Id = id,
            Destination = "user@test.com",
            Message = "hello",
            Priority = NotificationPriority.High,
            Type = NotificationType.Email
        };

        // Assert
        Assert.Equal(id, notification.Id);
        Assert.Equal("user@test.com", notification.Destination);
        Assert.Equal("hello", notification.Message);
        Assert.Equal(NotificationPriority.High, notification.Priority);
        Assert.Equal(NotificationType.Email, notification.Type);
    }
}
