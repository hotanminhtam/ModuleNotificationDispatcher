using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher;

internal class Program
{
    /// <summary>
    /// Entry point: creates sample notifications and dispatches them concurrently.
    /// </summary>
    static async Task Main()
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("    MODULE NOTIFICATION DISPATCHER");
        Console.WriteLine("    Multi-channel notification processing (Email, SMS)");
        Console.WriteLine("==============================================================\n");

        // --- CancellationToken: used to cancel everything if Ctrl+C is pressed ---
        using var cts = new CancellationTokenSource();

        // When the user presses Ctrl+C → cancel the entire process
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[CANCEL] Shutting down gracefully...");
        };

        // --- Create the Dispatcher (main processing engine) ---
        // perRequestTimeout = 30s: each notification must complete within 30 seconds
        // maxParallelism = 100: process up to 100 notifications concurrently
        // maxRetry = 3: retry up to 3 times on failure
        var dispatcher = new NotificationDispatcher(
            perRequestTimeout: TimeSpan.FromSeconds(30),
            maxParallelism: 100,
            maxRetry: 3);

        // --- Generate 1000 sample notifications ---
        Console.WriteLine("[SETUP] Generating 1000 sample notifications...\n");

        var notifications = new List<Notification>();
        for (int i = 0; i < 1000; i++)
        {
            // Alternate between Email and SMS
            bool isEmail = i % 2 == 0;

            // Rotate priority: High → Medium → Low → High → ...
            var priority = (i % 3) switch
            {
                0 => NotificationPriority.High,    // OTP, Alerts
                1 => NotificationPriority.Medium,  // Transactions
                _ => NotificationPriority.Low      // Marketing
            };

            notifications.Add(new Notification
            {
                Destination = isEmail ? $"user{i}@mail.com" : $"+8490000{i:D4}",
                Message = $"Notification #{i} - Priority: {priority}",
                Type = isEmail ? NotificationType.Email : NotificationType.Sms,
                Priority = priority
            });
        }

        // --- Dispatch all notifications ---
        await dispatcher.DispatchAsync(notifications, cts.Token);

        Console.WriteLine("Thank you for using ModuleNotificationDispatcher.");
    }
}