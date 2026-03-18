using System.Text.Json;
using Confluent.Kafka;
using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Kafka;

/// <summary>
/// Consumes notification messages from a Kafka topic and dispatches them
/// through the NotificationDispatcher pipeline.
/// </summary>
public class KafkaConsumerService
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly NotificationDispatcher _dispatcher;

    /// <summary>
    /// Initializes the consumer with Kafka settings, providers, and dispatcher settings.
    /// </summary>
    /// <param name="kafkaSettings">Kafka connection and topic configuration.</param>
    /// <param name="providers">Notification providers to use for dispatching.</param>
    /// <param name="dispatcherSettings">Dispatcher configuration (timeout, parallelism, retry).</param>
    public KafkaConsumerService(
        KafkaSettings kafkaSettings,
        IEnumerable<INotificationProvider> providers,
        DispatcherSettings dispatcherSettings)
    {
        _kafkaSettings = kafkaSettings;
        _dispatcher = new NotificationDispatcher(
            providers,
            TimeSpan.FromSeconds(dispatcherSettings.PerRequestTimeoutSeconds),
            dispatcherSettings.MaxParallelism,
            dispatcherSettings.MaxRetry);
    }

    /// <summary>
    /// Starts consuming messages from Kafka and dispatching them in batches.
    /// </summary>
    /// <param name="batchSize">Number of messages to collect before dispatching.</param>
    /// <param name="batchTimeout">Max time to wait for a full batch.</param>
    /// <param name="ct">Cancellation token to stop the consumer.</param>
    public async Task ConsumeAsync(int batchSize, TimeSpan batchTimeout, CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_kafkaSettings.AutoOffsetReset, ignoreCase: true),
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_kafkaSettings.Topic);

        Console.WriteLine($"[KAFKA] Subscribed to topic '{_kafkaSettings.Topic}' " +
                          $"(group: {_kafkaSettings.GroupId})");
        Console.WriteLine($"[KAFKA] Batch size: {batchSize}, Batch timeout: {batchTimeout.TotalSeconds}s\n");

        while (!ct.IsCancellationRequested)
        {
            var batch = CollectBatch(consumer, batchSize, batchTimeout, ct);

            if (batch.Count == 0)
                continue;

            Console.WriteLine($"[KAFKA] Collected batch of {batch.Count} notifications");

            try
            {
                await _dispatcher.DispatchAsync(batch, ct);
                consumer.Commit();
                Console.WriteLine("[KAFKA] Batch committed.\n");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n[KAFKA] Consumer cancelled.");
                break;
            }
        }

        consumer.Close();
        Console.WriteLine("[KAFKA] Consumer closed.");
    }

    /// <summary>
    /// Collects a batch of notifications from Kafka, up to batchSize or batchTimeout.
    /// </summary>
    private static List<Notification> CollectBatch(
        IConsumer<Ignore, string> consumer,
        int batchSize,
        TimeSpan batchTimeout,
        CancellationToken ct)
    {
        var batch = new List<Notification>(batchSize);
        var deadline = DateTime.UtcNow + batchTimeout;

        while (batch.Count < batchSize && DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                var result = consumer.Consume(remaining);
                if (result?.Message?.Value == null)
                    continue;

                var notification = JsonSerializer.Deserialize<Notification>(result.Message.Value);
                if (notification != null)
                    batch.Add(notification);
            }
            catch (ConsumeException ex)
            {
                Console.WriteLine($"[KAFKA] Consume error: {ex.Error.Reason}");
            }
        }

        return batch;
    }
}
