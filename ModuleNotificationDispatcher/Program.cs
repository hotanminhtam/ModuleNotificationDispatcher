using Microsoft.Extensions.Configuration;
using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Providers;

namespace ModuleNotificationDispatcher;

internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("    MODULE NOTIFICATION DISPATCHER");
        Console.WriteLine("    Multi-channel notification processing (Email, SMS)");
        Console.WriteLine("==============================================================\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var settings = new DispatcherSettings();
        configuration.GetSection("DispatcherSettings").Bind(settings);

        Console.WriteLine($"[CONFIG] Timeout={settings.PerRequestTimeoutSeconds}s, " +
                          $"Parallelism={settings.MaxParallelism}, " +
                          $"Retry={settings.MaxRetry}, " +
                          $"Count={settings.NotificationCount}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[CANCEL] Shutting down gracefully...");
        };

        INotificationProvider[] providers =
        [
            new EmailNotificationProvider(),
            new SmsNotificationProvider()
        ];

        var dispatcher = new NotificationDispatcher(
            providers: providers,
            perRequestTimeout: TimeSpan.FromSeconds(settings.PerRequestTimeoutSeconds),
            maxParallelism: settings.MaxParallelism,
            maxRetry: settings.MaxRetry);

        Console.WriteLine("\n[SETUP] Generating sample notifications...\n");

        var notifications = new List<Notification>();
        for (int i = 0; i < settings.NotificationCount; i++)
        {
            bool isEmail = i % 2 == 0;
            var priority = (i % 3) switch
            {
                0 => NotificationPriority.High,
                1 => NotificationPriority.Medium,
                _ => NotificationPriority.Low
            };

            notifications.Add(new Notification
            {
                Destination = isEmail ? $"user{i}@mail.com" : $"+8490000{i:D4}",
                Message = $"Notification #{i} - Priority: {priority}",
                Type = isEmail ? NotificationType.Email : NotificationType.Sms,
                Priority = priority
            });
        }

        try
        {
            await dispatcher.DispatchAsync(notifications, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[CANCELLED] Dispatch was cancelled.");
        }

        Console.WriteLine("Thank you for using ModuleNotificationDispatcher.");
    }
}