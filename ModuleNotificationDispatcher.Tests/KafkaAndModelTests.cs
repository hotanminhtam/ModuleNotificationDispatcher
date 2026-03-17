using Confluent.Kafka;
using Moq;
using ModuleNotificationDispatcher.Infrastructure.Kafka;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Application;
using ModuleNotificationDispatcher.Domain.Interfaces;
using System.Text.Json;
using Xunit;

namespace ModuleNotificationDispatcher.Tests;

public class KafkaComponentTests
{
    [Fact]
    public async Task Producer_ProduceAsync_ShouldCallUnderlyingProducer()
    {
        // Arrange
        var mockInternalProducer = new Mock<IProducer<string, string>>();
        var topic = "test-topic";
        var producer = new NotificationProducer(mockInternalProducer.Object, topic);
        var notification = new Notification { Destination = "test@example.com", Message = "test message" };

        // Act
        await producer.ProduceAsync(notification);

        // Assert
        mockInternalProducer.Verify(p => p.ProduceAsync(
            topic, 
            It.Is<Message<string, string>>(m => m.Key == notification.Id.ToString() && m.Value.Contains("test message")),
            default), Times.Once);
    }

    [Fact]
    public async Task Consumer_StartConsumingAsync_ShouldDispatchWhenMessageReceived()
    {
        // Arrange
        var mockInternalConsumer = new Mock<IConsumer<string, string>>();
        
        // Use real dispatcher with mock provider to ensure actual logic is executed
        var mockProvider = new Mock<INotificationProvider>();
        mockProvider.Setup(p => p.Type).Returns(NotificationType.Email);
        mockProvider.Setup(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        var dispatcher = new NotificationDispatcher(new[] { mockProvider.Object });

        var topic = "test-topic";
        var consumer = new NotificationConsumer(mockInternalConsumer.Object, topic, dispatcher);
        
        var notification = new Notification { Type = NotificationType.Email, Destination = "target" };
        var json = JsonSerializer.Serialize(notification);
        var consumeResult = new ConsumeResult<string, string>
        {
            Message = new Message<string, string> { Value = json },
            Topic = topic,
            Partition = 0,
            Offset = 0
        };

        var cts = new CancellationTokenSource();
        int callCount = 0;
        
        mockInternalConsumer.Setup(c => c.Consume(It.IsAny<CancellationToken>()))
                            .Returns(() => {
                                if (++callCount > 1) 
                                {
                                    cts.Cancel();
                                    throw new OperationCanceledException();
                                }
                                return consumeResult;
                            });

        // Act
        await consumer.StartConsumingAsync(cts.Token);

        // Assert
        mockProvider.Verify(p => p.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

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
