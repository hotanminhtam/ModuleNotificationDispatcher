namespace ModuleNotificationDispatcher.Domain.Models;

/// <summary>
/// Configuration settings for Kafka consumer/producer, mapped from appsettings.json.
/// </summary>
public class KafkaSettings
{
    /// <summary>Kafka broker address (e.g., "localhost:9092").</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Kafka topic to consume/produce notifications.</summary>
    public string Topic { get; set; } = "notifications";

    /// <summary>Consumer group ID.</summary>
    public string GroupId { get; set; } = "notification-dispatcher-group";

    /// <summary>Where to start reading if no committed offset ("Earliest" or "Latest").</summary>
    public string AutoOffsetReset { get; set; } = "Earliest";

    /// <summary>Number of messages to collect before dispatching a batch.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Max seconds to wait for a full batch before dispatching.</summary>
    public int BatchTimeoutSeconds { get; set; } = 5;

    /// <summary>Default number of sample messages to produce in produce mode.</summary>
    public int DefaultProduceCount { get; set; } = 100;
}
