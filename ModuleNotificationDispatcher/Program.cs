using Microsoft.Extensions.Configuration;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Kafka;
using ModuleNotificationDispatcher.Infrastructure.Providers;

namespace ModuleNotificationDispatcher;

/// <summary>
/// Entry point: supports produce and consume modes via command-line argument.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("    MODULE NOTIFICATION DISPATCHER");
        Console.WriteLine("    Multi-channel notification processing (Email, SMS)");
        Console.WriteLine("==============================================================\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var dispatcherSettings = new DispatcherSettings();
        configuration.GetSection("DispatcherSettings").Bind(dispatcherSettings);

        var kafkaSettings = new KafkaSettings();
        configuration.GetSection("KafkaSettings").Bind(kafkaSettings);

        var mode = args.Length > 0 ? args[0].ToLower() : "consume";

        Console.WriteLine($"[CONFIG] Timeout={dispatcherSettings.PerRequestTimeoutSeconds}s, " +
                          $"Parallelism={dispatcherSettings.MaxParallelism}, " +
                          $"Retry={dispatcherSettings.MaxRetry}");
        Console.WriteLine($"[CONFIG] Broker={kafkaSettings.BootstrapServers}, " +
                          $"Topic={kafkaSettings.Topic}");
        Console.WriteLine($"[CONFIG] Mode={mode}\n");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[CANCEL] Shutting down gracefully...");
        };

        switch (mode)
        {
            case "produce":
                int count = args.Length > 1 && int.TryParse(args[1], out var c) ? c : kafkaSettings.DefaultProduceCount;
                await KafkaProducerHelper.ProduceAsync(kafkaSettings, count);
                break;

            case "consume":
                INotificationProvider[] providers =
                [
                    new EmailNotificationProvider(),
                    new SmsNotificationProvider()
                ];

                var consumer = new KafkaConsumerService(
                    kafkaSettings, providers, dispatcherSettings);
                await consumer.ConsumeAsync(
                    batchSize: kafkaSettings.BatchSize,
                    batchTimeout: TimeSpan.FromSeconds(kafkaSettings.BatchTimeoutSeconds),
                    ct: cts.Token);
                break;

            default:
                Console.WriteLine("Usage:");
                Console.WriteLine("  dotnet run -- produce [count]   Produce sample notifications to Kafka");
                Console.WriteLine("  dotnet run -- consume           Consume from Kafka & dispatch");
                break;
        }

        Console.WriteLine("\nThank you for using ModuleNotificationDispatcher.");
    }
}