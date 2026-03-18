using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher;

internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("    MODULE NOTIFICATION DISPATCHER");
        Console.WriteLine("    Multi-channel notification processing (Email, SMS)");
        Console.WriteLine("==============================================================\n");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[CANCEL] Shutting down gracefully...");
        };

        var dispatcher = new NotificationDispatcher(
            perRequestTimeout: TimeSpan.FromSeconds(30),
            maxParallelism: 100,
            maxRetry: 3);

        Console.WriteLine("[SETUP] Generating sample notifications...\n");

        var notifications = new List<Notification>();
        for (int i = 0; i < 5000; i++)
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