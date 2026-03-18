using ModuleNotificationDispatcher.Infrastructure.Resilience;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Providers;
using ModuleNotificationDispatcher.Application.Validation;
using ModuleNotificationDispatcher.Domain.Interfaces;

namespace ModuleNotificationDispatcher.Application.Dispatcher;

/// <summary>
/// Dispatches thousands of notifications concurrently.
/// Supports: PriorityQueue, Retry with Exponential Backoff, Timeout, Validation.
/// </summary>
public class NotificationDispatcher
{
    // Available providers (Email, SMS) mapped by their notification type
    private readonly Dictionary<NotificationType, INotificationProvider> _providers;

    // Maximum time allowed per notification (default: 30 seconds)
    private readonly TimeSpan _perRequestTimeout;

    // Maximum number of notifications processed in parallel
    private readonly int _maxParallelism;

    // Maximum retry attempts when sending fails
    private readonly int _maxRetry;

    /// <summary>
    /// Initializes the Dispatcher with the required settings.
    /// </summary>
    public NotificationDispatcher(
        IEnumerable<INotificationProvider>? providers = null,
        TimeSpan? perRequestTimeout = null,
        int maxParallelism = 5000,
        int maxRetry = 3)
    {
        // If no providers are given, use Email + SMS by default
        providers ??= [new EmailNotificationProvider(), new SmsNotificationProvider()];
        _providers = providers.ToDictionary(p => p.Type);

        _perRequestTimeout = perRequestTimeout ?? TimeSpan.FromSeconds(30);
        _maxParallelism = maxParallelism;
        _maxRetry = maxRetry;
    }

    /// <summary>
    /// Dispatches a batch of notifications concurrently, prioritized by urgency.
    ///
    /// Processing flow:
    /// 1. Enqueue all notifications into a PriorityQueue (High=1 first, Low=3 last)
    /// 2. Dequeue in priority order
    /// 3. Process in parallel using Parallel.ForEachAsync
    /// 4. Each notification: Validate → Find Provider → Retry up to 3 times
    /// </summary>
    public async Task DispatchAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct)
    {
        // ===== STEP 1: Enqueue into PriorityQueue =====
        // PriorityQueue automatically sorts: lower number = higher priority
        // High=1 will be dequeued first, Low=3 last
        var priorityQueue = new PriorityQueue<Notification, int>();

        foreach (var notification in notifications)
        {
            // Enqueue: add notification with its priority as the sort key (1, 2, or 3)
            priorityQueue.Enqueue(notification, (int)notification.Priority);
        }

        // ===== STEP 2: Dequeue in priority order =====
        // Dequeue one by one → High first, then Medium, then Low
        var sortedNotifications = new List<Notification>(priorityQueue.Count);
        while (priorityQueue.Count > 0)
        {
            sortedNotifications.Add(priorityQueue.Dequeue());
        }

        // ===== STEP 3: Process in parallel =====
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParallelism  // Limit concurrent threads
        };

        // Result counters (using Interlocked because multiple threads update them)
        long successCount = 0, failureCount = 0, timeoutCount = 0;

        Console.WriteLine($"--- Starting dispatch for {sortedNotifications.Count} notifications ---");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Parallel.ForEachAsync: processes many notifications AT THE SAME TIME (not sequentially)
        await Parallel.ForEachAsync(sortedNotifications, parallelOptions, async (notification, _) =>
        {
            // Process each notification: validate → send → retry on failure
            var result = await ProcessOneNotificationAsync(notification, ct);

            // Update counters based on result
            switch (result)
            {
                case Result.Success:
                    Interlocked.Increment(ref successCount);
                    break;
                case Result.Timeout:
                    Interlocked.Increment(ref timeoutCount);
                    break;
                default: // Failure or Invalid
                    Interlocked.Increment(ref failureCount);
                    break;
            }
        });

        watch.Stop();
        PrintSummary(watch.Elapsed.TotalSeconds, successCount, failureCount, timeoutCount);
    }

    /// <summary>
    /// Processes a SINGLE notification:
    /// 1. Validate data (is the email/phone format correct?)
    /// 2. Find the matching provider (EmailProvider or SmsProvider)
    /// 3. Send with retry up to 3 times (Exponential Backoff)
    /// 4. If timeout exceeded → return Timeout
    /// </summary>
    private async Task<Result> ProcessOneNotificationAsync(
        Notification notification,
        CancellationToken ct)
    {
        // Step 1: Validate the notification data
        var validation = NotificationValidator.Validate(notification);
        if (!validation.IsValid)
        {
            Console.WriteLine($"[INVALID] {notification.Id} - {validation.ErrorMessage}");
            return Result.Failure;
        }

        // Step 2: Find provider (Email → EmailProvider, Sms → SmsProvider)
        if (!_providers.TryGetValue(notification.Type, out var provider))
        {
            Console.WriteLine($"[ERROR] No provider found for type: {notification.Type}");
            return Result.Failure;
        }

        // Step 3: Create a per-notification timeout (default 30 seconds)
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_perRequestTimeout);

        try
        {
            // Step 4: Send with Retry (up to _maxRetry attempts)
            // Attempt 1 fails → wait 500ms  → Attempt 2
            // Attempt 2 fails → wait 1000ms → Attempt 3
            // Attempt 3 fails → wait 2000ms → Attempt 4 (final)
            // Attempt 4 fails → throw exception
            await Retry.ExecuteAsync(
                action: () => provider.SendAsync(notification, timeoutCts.Token),
                cancellationToken: timeoutCts.Token,
                maxRetry: _maxRetry);

            return Result.Success;
        }
        catch (OperationCanceledException)
        {
            // Cancelled due to timeout or Ctrl+C
            return Result.Timeout;
        }
        catch
        {
            // All retry attempts exhausted, still failing
            return Result.Failure;
        }
    }

    /// <summary>
    /// Prints a summary report after dispatch is complete.
    /// </summary>
    private static void PrintSummary(double totalSeconds, long success, long failure, long timeout)
    {
        Console.WriteLine("\n==========================================");
        Console.WriteLine("     NOTIFICATION DISPATCH SUMMARY");
        Console.WriteLine($"  Total Time: {totalSeconds:F2}s");
        Console.WriteLine($"  Success:    {success}");
        Console.WriteLine($"  Failure:    {failure}");
        Console.WriteLine($"  Timeout:    {timeout}");
        Console.WriteLine("==========================================\n");
    }

    // Simple enum to classify the result of processing a notification
    private enum Result { Success, Failure, Timeout }
}
