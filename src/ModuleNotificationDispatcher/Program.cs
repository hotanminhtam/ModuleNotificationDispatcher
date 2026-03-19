using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModuleNotificationDispatcher.Application;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure;
using ModuleNotificationDispatcher.Infrastructure.Kafka;

namespace ModuleNotificationDispatcher;

/// <summary>
/// Entry point: supports produce and consume modes via command-line argument.
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                // Bind settings
                var dispatcherSettings = new DispatcherSettings();
                context.Configuration.GetSection("DispatcherSettings").Bind(dispatcherSettings);
                services.AddSingleton(dispatcherSettings);

                var kafkaSettings = new KafkaSettings();
                context.Configuration.GetSection("KafkaSettings").Bind(kafkaSettings);
                services.AddSingleton(kafkaSettings);

                // Register layers
                services.AddApplication();
                services.AddInfrastructure();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        var dispatcherSettings = host.Services.GetRequiredService<DispatcherSettings>();
        var kafkaSettings = host.Services.GetRequiredService<KafkaSettings>();

        var mode = args.Length > 0 ? args[0].ToLower() : "consume";

        logger.LogInformation("MODULE NOTIFICATION DISPATCHER");
        logger.LogInformation(
            "Config — Timeout={Timeout}s, Parallelism={Parallelism}, Retry={Retry}",
            dispatcherSettings.PerRequestTimeoutSeconds,
            dispatcherSettings.MaxParallelism,
            dispatcherSettings.MaxRetry);
        logger.LogInformation(
            "Config — Broker={Broker}, Topic={Topic}, Mode={Mode}",
            kafkaSettings.BootstrapServers, kafkaSettings.Topic, mode);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            logger.LogWarning("Shutting down gracefully...");
        };

        switch (mode)
        {
            case "produce":
                int count = args.Length > 1 && int.TryParse(args[1], out var c)
                    ? c
                    : kafkaSettings.DefaultProduceCount;
                var producer = host.Services.GetRequiredService<KafkaProducerHelper>();
                await producer.ProduceAsync(count);
                break;

            case "consume":
                var consumer = host.Services.GetRequiredService<KafkaConsumerService>();
                await consumer.ConsumeAsync(
                    batchSize: kafkaSettings.BatchSize,
                    batchTimeout: TimeSpan.FromSeconds(kafkaSettings.BatchTimeoutSeconds),
                    ct: cts.Token);
                break;

            default:
                logger.LogWarning(
                    "Unknown mode '{Mode}'. Usage: dotnet run -- produce [count] | dotnet run -- consume",
                    mode);
                break;
        }

        logger.LogInformation("ModuleNotificationDispatcher finished.");
    }
}