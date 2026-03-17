using Microsoft.Extensions.Configuration;
using ModuleNotificationDispatcher.Application;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Kafka;

namespace ModuleNotificationDispatcher;

internal class Program
{
    /// <summary>
    /// Entry point for the Notification system with Kafka integration.
    /// Supports both producing (pushing to Kafka) and consuming (sending notifications).
    /// </summary>
    private static async Task Main()
    {
        // --- 1. CONFIGURATION SETUP ---
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = config.GetSection("NotificationSettings");
        var kafkaSettings = config.GetSection("KafkaSettings");

        // Load dispatch settings
        int perRequestTimeout = settings.GetValue<int>("PerRequestTimeoutSeconds", 30);
        int maxParallelism = settings.GetValue<int>("MaxParallelism", 5000);
        int minThreads = settings.GetValue<int>("MinThreads", 2000);
        int maxRetry = settings.GetValue<int>("MaxRetry", 3);

        // Load Kafka settings
        string bootstrapServers = kafkaSettings.GetValue<string>("BootstrapServers", "localhost:9092")!;
        string topic = kafkaSettings.GetValue<string>("Topic", "notification-requests")!;
        string groupId = kafkaSettings.GetValue<string>("GroupId", "notification-group")!;

        // --- 2. PERFORMANCE OPTIMIZATION ---
        // Pre-warm the ThreadPool to handle massive concurrency without initial delay
        ThreadPool.SetMinThreads(minThreads, minThreads);

        Console.WriteLine("==============================================================");
        Console.WriteLine(" MODULE NOTIFICATION DISPATCHER (KAFKA EDITION)");
        Console.WriteLine("==============================================================\n");
        Console.WriteLine("Choose Mode:");
        Console.WriteLine(" [1] Producer (Generate & Push 10k messages to Kafka)");
        Console.WriteLine(" [2] Consumer (Listen to Kafka & Dispatch notifications)");
        Console.Write("\nYour choice: ");
        
        var choice = Console.ReadLine();
        using CancellationTokenSource cts = new();

        // Handle Ctrl+C for graceful shutdown
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nShutting down gracefully...");
        };

        if (choice == "1")
        {
            // --- PRODUCER MODE: Messaging Simulator ---
            using var producer = new NotificationProducer(bootstrapServers, topic);
            Console.WriteLine($"\n[PRODUCER] Generating 10,000 notifications for topic '{topic}'...");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                var type = i % 2 == 0 ? NotificationType.Email : NotificationType.Sms;
                var notification = new Notification
                {
                    Destination = type == NotificationType.Email ? $"user{i}@mail.com" : $"09876543{i%100:D2}",
                    Message = $"Performance Test Message #{i} - Sent via Kafka",
                    Type = type,
                    Priority = (NotificationPriority)((i % 3) + 1)
                };

                await producer.ProduceAsync(notification);
            }
            watch.Stop();
            Console.WriteLine($"\n[SUCCESS] Successfully pushed 10,000 messages in {watch.Elapsed.TotalSeconds:F2}s.");
        }
        else
        {
            // --- CONSUMER MODE: Real-time Dispatcher ---
            NotificationDispatcher dispatcher = new(
                perRequestTimeout: TimeSpan.FromSeconds(perRequestTimeout),
                maxParallelism: maxParallelism,
                maxRetry: maxRetry);

            var consumer = new NotificationConsumer(bootstrapServers, topic, groupId, dispatcher);
            
            Console.WriteLine($"\n[CONSUMER] Listening on brokers: {bootstrapServers}");
            Console.WriteLine("[CONSUMER] Press Ctrl+C to exit.");
            
            await consumer.StartConsumingAsync(cts.Token);
        }

        Console.WriteLine("\nThank you for using ModuleNotificationDispatcher.");
    }
}