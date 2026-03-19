using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<KafkaConsumerService> _logger;

    /// <summary>
    /// Initializes the consumer with Kafka settings, dispatcher, and logger.
    /// </summary>
    /// <param name="kafkaSettings">Kafka connection and topic configuration.</param>
    /// <param name="dispatcher">Dispatcher pipeline for processing notifications.</param>
    /// <param name="logger">Logger instance for structured output.</param>
    public KafkaConsumerService(
        KafkaSettings kafkaSettings,
        NotificationDispatcher dispatcher,
        ILogger<KafkaConsumerService> logger)
    {
        _kafkaSettings = kafkaSettings;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Starts consuming messages from Kafka and dispatching them in batches.
    /// </summary>
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

        _logger.LogInformation(
            "Subscribed to topic '{Topic}' (group: {GroupId}) | Batch: {BatchSize}, Timeout: {Timeout}s",
            _kafkaSettings.Topic, _kafkaSettings.GroupId, batchSize, batchTimeout.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            var batch = CollectBatch(consumer, batchSize, batchTimeout, ct);

            if (batch.Count == 0)
                continue;

            _logger.LogInformation("Collected batch of {Count} notifications", batch.Count);

            try
            {
                await _dispatcher.DispatchAsync(batch, ct);
                consumer.Commit();
                _logger.LogInformation("Batch committed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Consumer cancelled");
                break;
            }
        }

        consumer.Close();
        _logger.LogInformation("Consumer closed");
    }

    /// <summary>
    /// Collects a batch of notifications from Kafka, up to batchSize or batchTimeout.
    /// </summary>
    private List<Notification> CollectBatch(
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
                _logger.LogError("Consume error: {Reason}", ex.Error.Reason);
            }
        }

        return batch;
    }
}
