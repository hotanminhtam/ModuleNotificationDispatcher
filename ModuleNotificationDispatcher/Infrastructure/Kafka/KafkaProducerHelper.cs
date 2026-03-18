using System.Text.Json;
using Confluent.Kafka;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher.Infrastructure.Kafka;

/// <summary>
/// Helper to produce sample notification messages to a Kafka topic for testing.
/// </summary>
public static class KafkaProducerHelper
{
    /// <summary>
    /// Produces a batch of sample notification messages to the configured Kafka topic.
    /// </summary>
    /// <param name="kafkaSettings">Kafka connection and topic configuration.</param>
    /// <param name="count">Number of messages to produce.</param>
    public static async Task ProduceAsync(KafkaSettings kafkaSettings, int count)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.BootstrapServers
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        Console.WriteLine($"[PRODUCER] Sending {count} notifications to topic '{kafkaSettings.Topic}'...\n");

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
                kafkaSettings.Topic,
                new Message<Null, string> { Value = json });

            if ((i + 1) % 100 == 0)
                Console.WriteLine($"  Produced {i + 1}/{count} messages");
        }

        producer.Flush(TimeSpan.FromSeconds(10));
        Console.WriteLine($"\n[PRODUCER] Done. {count} messages sent to '{kafkaSettings.Topic}'.");
    }
}
