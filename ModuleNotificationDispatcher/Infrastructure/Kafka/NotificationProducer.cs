using Confluent.Kafka;
using System.Text.Json;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Kafka;

/// <summary>
/// Handles pushing notification messages into the Kafka message bus.
/// </summary>
public class NotificationProducer : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    /// <summary>
    /// Initializes a new producer with default configuration.
    /// </summary>
    /// <param name="bootstrapServers">Kafka broker addresses.</param>
    /// <param name="topic">Destination topic for notifications.</param>
    public NotificationProducer(string bootstrapServers, string topic)
        : this(new ProducerBuilder<string, string>(new ProducerConfig 
        { 
            BootstrapServers = bootstrapServers,
            LingerMs = 5,
            Acks = Acks.Leader,
            Debug = "broker,topic,msg", // Enable detailed debugging
            MessageTimeoutMs = 10000     // Set a 10s timeout for messages
        })
        .SetErrorHandler((_, e) => Console.WriteLine($"Kafka Error: {e.Reason}"))
        .SetLogHandler((_, l) => Console.WriteLine($"Kafka Log: [{l.Level}] {l.Message}"))
        .Build(), topic)
    {
    }

    /// <summary>
    /// Initializes a new producer with a specific IProducer (useful for testing).
    /// </summary>
    /// <param name="producer">The underlying Kafka producer.</param>
    /// <param name="topic">Destination topic.</param>
    public NotificationProducer(IProducer<string, string> producer, string topic)
    {
        _producer = producer;
        _topic = topic;
    }

    /// <summary>
    /// Publishes a notification to the Kafka topic.
    /// </summary>
    public async Task ProduceAsync(Notification notification)
    {
        var value = JsonSerializer.Serialize(notification);
        var message = new Message<string, string> 
        { 
            Key = notification.Id.ToString(), 
            Value = value 
        };

        await _producer.ProduceAsync(_topic, message);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
