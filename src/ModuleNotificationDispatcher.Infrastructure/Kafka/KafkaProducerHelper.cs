using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Kafka;

/// <summary>
/// Helper to produce sample notification messages to a Kafka topic for testing.
/// </summary>
public class KafkaProducerHelper
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly ILogger<KafkaProducerHelper> _logger;

    /// <summary>
    /// Initializes a new instance with Kafka settings and logger.
    /// </summary>
    /// <param name="kafkaSettings">Kafka connection and topic configuration.</param>
    /// <param name="logger">Logger instance for structured output.</param>
    public KafkaProducerHelper(KafkaSettings kafkaSettings, ILogger<KafkaProducerHelper> logger)
    {
        _kafkaSettings = kafkaSettings;
        _logger = logger;
    }

    /// <summary>
    /// Produces a batch of sample notification messages to the configured Kafka topic.
    /// </summary>
    /// <param name="count">Number of messages to produce.</param>
    public async Task ProduceAsync(int count)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        _logger.LogInformation("Sending {Count} notifications to topic '{Topic}'...", count, _kafkaSettings.Topic);

        for (int i = 0; i < count; i++)
        {
            bool isEmail = i % 2 == 0;
            var priority = (i % 3) switch
            {
                0 => NotificationPriority.High,
                1 => NotificationPriority.Medium,
                _ => NotificationPriority.Low
            };

            var notification = new Notification
            {
                Destination = isEmail ? $"user{i}@mail.com" : $"+8490000{i:D4}",
                Message = $"Kafka Notification #{i} - Priority: {priority}",
                Type = isEmail ? NotificationType.Email : NotificationType.Sms,
                Priority = priority
            };

            var json = JsonSerializer.Serialize(notification);
            await producer.ProduceAsync(
                _kafkaSettings.Topic,
                new Message<Null, string> { Value = json });

            if ((i + 1) % 100 == 0)
                _logger.LogInformation("Produced {Current}/{Total} messages", i + 1, count);
        }

        producer.Flush(TimeSpan.FromSeconds(10));
        _logger.LogInformation("Done. {Count} messages sent to '{Topic}'", count, _kafkaSettings.Topic);
    }
}
