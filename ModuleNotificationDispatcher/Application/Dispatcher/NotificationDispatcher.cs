using ModuleNotificationDispatcher.Infrastructure.Resilience;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Providers;
using ModuleNotificationDispatcher.Application.Validation;
using ModuleNotificationDispatcher.Domain.Interfaces;

namespace ModuleNotificationDispatcher.Application.Dispatcher;

/// <summary>
/// Xử lý việc gửi hàng nghìn notification cùng lúc (song song).
/// Hỗ trợ: PriorityQueue, Retry, Timeout, Validation.
/// </summary>
public class NotificationDispatcher
{
    // Danh sách các provider (Email, SMS) để gửi notification
    private readonly Dictionary<NotificationType, INotificationProvider> _providers;

    // Timeout tối đa cho MỖI notification (mặc định 30 giây)
    private readonly TimeSpan _perRequestTimeout;

    // Số notification được xử lý song song tối đa
    private readonly int _maxParallelism;

    // Số lần thử lại tối đa khi gửi thất bại
    private readonly int _maxRetry;

    /// <summary>
    /// Khởi tạo Dispatcher với các thiết lập cần thiết.
    /// </summary>
    public NotificationDispatcher(
        IEnumerable<INotificationProvider>? providers = null,
        TimeSpan? perRequestTimeout = null,
        int maxParallelism = 5000,
        int maxRetry = 3)
    {
        // Nếu không truyền provider, mặc định dùng Email + SMS
        providers ??= [new EmailNotificationProvider(), new SmsNotificationProvider()];
        _providers = providers.ToDictionary(p => p.Type);

        _perRequestTimeout = perRequestTimeout ?? TimeSpan.FromSeconds(30);
        _maxParallelism = maxParallelism;
        _maxRetry = maxRetry;
    }

    /// <summary>
    /// Gửi hàng loạt notification song song, ưu tiên theo Priority.
    /// 
    /// Luồng xử lý:
    /// 1. Đưa tất cả notification vào PriorityQueue (High=1 xử lý trước, Low=3 xử lý sau)
    /// 2. Lấy ra từ queue theo thứ tự ưu tiên
    /// 3. Xử lý song song bằng Parallel.ForEachAsync
    /// 4. Mỗi notification: Validate → Tìm Provider → Retry gửi (tối đa 3 lần)
    /// </summary>
    public async Task DispatchAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct)
    {
        // ===== BƯỚC 1: Đưa vào PriorityQueue =====
        // PriorityQueue tự động sắp xếp: số nhỏ hơn = ưu tiên cao hơn
        // High=1 sẽ được lấy ra trước, Low=3 lấy ra sau
        var priorityQueue = new PriorityQueue<Notification, int>();

        foreach (var notification in notifications)
        {
            // Enqueue: thêm notification vào queue với priority là số (1, 2, hoặc 3)
            priorityQueue.Enqueue(notification, (int)notification.Priority);
        }

        // ===== BƯỚC 2: Lấy ra theo thứ tự ưu tiên =====
        // Dequeue lần lượt → High ra trước, Medium tiếp, Low cuối
        var sortedNotifications = new List<Notification>(priorityQueue.Count);
        while (priorityQueue.Count > 0)
        {
            sortedNotifications.Add(priorityQueue.Dequeue());
        }

        // ===== BƯỚC 3: Xử lý song song =====
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParallelism  // Giới hạn số luồng chạy cùng lúc
        };

        // Bộ đếm kết quả (dùng Interlocked vì nhiều thread cùng cập nhật)
        long successCount = 0, failureCount = 0, timeoutCount = 0;

        Console.WriteLine($"--- Bắt đầu gửi {sortedNotifications.Count} notification ---");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Parallel.ForEachAsync: chạy nhiều notification CÙNG LÚC (không phải tuần tự)
        await Parallel.ForEachAsync(sortedNotifications, parallelOptions, async (notification, _) =>
        {
            // Xử lý từng notification: validate → gửi → retry nếu lỗi
            var result = await ProcessOneNotificationAsync(notification, ct);

            // Cập nhật bộ đếm dựa trên kết quả
            switch (result)
            {
                case Result.Success:
                    Interlocked.Increment(ref successCount);
                    break;
                case Result.Timeout:
                    Interlocked.Increment(ref timeoutCount);
                    break;
                default: // Failure hoặc Invalid
                    Interlocked.Increment(ref failureCount);
                    break;
            }
        });

        watch.Stop();
        PrintSummary(watch.Elapsed.TotalSeconds, successCount, failureCount, timeoutCount);
    }

    /// <summary>
    /// Xử lý MỘT notification đơn lẻ:
    /// 1. Validate dữ liệu (email/phone đúng format?)
    /// 2. Tìm provider phù hợp (EmailProvider hoặc SmsProvider)
    /// 3. Gửi với retry tối đa 3 lần (Exponential Backoff)
    /// 4. Nếu quá timeout → trả về Timeout
    /// </summary>
    private async Task<Result> ProcessOneNotificationAsync(
        Notification notification,
        CancellationToken ct)
    {
        // Bước 1: Kiểm tra dữ liệu hợp lệ
        var validation = NotificationValidator.Validate(notification);
        if (!validation.IsValid)
        {
            Console.WriteLine($"[INVALID] {notification.Id} - {validation.ErrorMessage}");
            return Result.Failure;
        }

        // Bước 2: Tìm provider (Email → EmailProvider, Sms → SmsProvider)
        if (!_providers.TryGetValue(notification.Type, out var provider))
        {
            Console.WriteLine($"[ERROR] Không tìm thấy provider cho loại: {notification.Type}");
            return Result.Failure;
        }

        // Bước 3: Tạo timeout riêng cho notification này (mặc định 30 giây)
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_perRequestTimeout);

        try
        {
            // Bước 4: Gửi notification với Retry (thử lại tối đa _maxRetry lần)
            // Nếu lần 1 lỗi → chờ 500ms → thử lần 2
            // Nếu lần 2 lỗi → chờ 1000ms → thử lần 3
            // Nếu lần 3 lỗi → throw exception
            await Retry.ExecuteAsync(
                action: () => provider.SendAsync(notification, timeoutCts.Token),
                cancellationToken: timeoutCts.Token,
                maxRetry: _maxRetry);

            return Result.Success;
        }
        catch (OperationCanceledException)
        {
            // Bị hủy do timeout hoặc Ctrl+C
            return Result.Timeout;
        }
        catch
        {
            // Đã retry hết số lần cho phép mà vẫn lỗi
            return Result.Failure;
        }
    }

    /// <summary>
    /// In bảng tổng kết sau khi gửi xong.
    /// </summary>
    private static void PrintSummary(double totalSeconds, long success, long failure, long timeout)
    {
        Console.WriteLine("\n==========================================");
        Console.WriteLine("       KẾT QUẢ GỬI NOTIFICATION");
        Console.WriteLine($"  Thời gian:    {totalSeconds:F2} giây");
        Console.WriteLine($"  Thành công:   {success}");
        Console.WriteLine($"  Thất bại:     {failure}");
        Console.WriteLine($"  Quá thời gian: {timeout}");
        Console.WriteLine("==========================================\n");
    }

    // Enum đơn giản để phân loại kết quả gửi
    private enum Result { Success, Failure, Timeout }
}
