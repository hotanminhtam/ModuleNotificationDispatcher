using ModuleNotificationDispatcher.Application.Dispatcher;
using ModuleNotificationDispatcher.Domain.Models;

namespace ModuleNotificationDispatcher;

internal class Program
{
    /// <summary>
    /// Chương trình chính: tạo danh sách notification mẫu và gửi song song.
    /// </summary>
    static async Task Main()
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("    MODULE NOTIFICATION DISPATCHER");
        Console.WriteLine("    Xử lý thông báo đa kênh (Email, SMS)");
        Console.WriteLine("==============================================================\n");

        // --- CancellationToken: dùng để hủy toàn bộ nếu nhấn Ctrl+C ---
        using var cts = new CancellationTokenSource();

        // Khi người dùng nhấn Ctrl+C → hủy toàn bộ tiến trình
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[HỦY] Đang dừng chương trình...");
        };

        // --- Tạo Dispatcher (bộ xử lý chính) ---
        // perRequestTimeout = 30 giây: mỗi notification không được quá 30 giây
        // maxParallelism = 100: xử lý tối đa 100 notification cùng lúc
        // maxRetry = 3: thử lại tối đa 3 lần nếu lỗi
        var dispatcher = new NotificationDispatcher(
            perRequestTimeout: TimeSpan.FromSeconds(30),
            maxParallelism: 100,
            maxRetry: 3);

        // --- Tạo 1000 notification mẫu ---
        Console.WriteLine("[CHUẨN BỊ] Tạo 1000 notification mẫu...\n");

        var notifications = new List<Notification>();
        for (int i = 0; i < 1000; i++)
        {
            // Xoay vòng: Email → SMS → Email → SMS ...
            bool isEmail = i % 2 == 0;

            // Xoay vòng Priority: High → Medium → Low → High → ...
            var priority = (i % 3) switch
            {
                0 => NotificationPriority.High,    // OTP, Alert
                1 => NotificationPriority.Medium,  // Giao dịch
                _ => NotificationPriority.Low      // Marketing
            };

            notifications.Add(new Notification
            {
                Destination = isEmail ? $"user{i}@mail.com" : $"+8490000{i:D4}",
                Message = $"Thông báo #{i} - Priority: {priority}",
                Type = isEmail ? NotificationType.Email : NotificationType.Sms,
                Priority = priority
            });
        }

        // --- Gửi tất cả notification ---
        await dispatcher.DispatchAsync(notifications, cts.Token);

        Console.WriteLine("Cảm ơn bạn đã sử dụng ModuleNotificationDispatcher.");
    }
}