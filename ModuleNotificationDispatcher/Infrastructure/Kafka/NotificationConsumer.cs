using Confluent.Kafka;
using System.Text.Json;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Application;

namespace ModuleNotificationDispatcher.Infrastructure.Kafka;

/// <summary>
/// Listens to Kafka topics and triggers the notification dispatch process.
/// </summary>
public class NotificationConsumer
{
    private readonly IConsumer<string, string> _consumer;
    private readonly NotificationDispatcher _dispatcher;
    private readonly string _topic;

    /// <summary>
    /// Initializes a new consumer with default configuration.
    /// </summary>
    /// <param name="bootstrapServers">Kafka broker addresses.</param>
    /// <param name="topic">Topic to listen to.</param>
    /// <param name="groupId">Consumer group identifier.</param>
    /// <param name="dispatcher">The dispatcher instance to process received messages.</param>
    public NotificationConsumer(
        string bootstrapServers, 
        string topic, 
        string groupId, 
        NotificationDispatcher dispatcher)
        : this(new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build(), topic, dispatcher)
    {
    }

    /// <summary>
    /// Initializes a new consumer with a specific IConsumer instance.
    /// </summary>
    /// <param name="consumer">The underlying Kafka consumer.</param>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="dispatcher">The dispatcher to process messages.</param>
    public NotificationConsumer(
        IConsumer<string, string> consumer, 
        string topic, 
        NotificationDispatcher dispatcher)
    {
        _consumer = consumer;
        _dispatcher = dispatcher;
        _topic = topic;
    }

    /// <summary>
    /// Starts the continuous consumption loop.
    /// </summary>
    public async Task StartConsumingAsync(CancellationToken ct)
    {
        _consumer.Subscribe(_topic);
        Console.WriteLine($"--- Kafka Consumer started: listening on topic '{_topic}' ---");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(ct);
                    if (result?.Message?.Value == null) continue;

                    var notification = JsonSerializer.Deserialize<Notification>(result.Message.Value);
                    if (notification != null)
                    {
                        // Process the notification using our high-performance Dispatcher
                        // For even higher throughput, you could batch multiple consumed messages here.
                        await _dispatcher.DispatchAsync(new[] { notification }, ct);
                    }
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occurred: {e.Error.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _consumer.Close();
        }
    }
}
