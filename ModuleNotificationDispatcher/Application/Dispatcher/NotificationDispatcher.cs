using ModuleNotificationDispatcher.Domain.Exceptions;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Resilience;

namespace ModuleNotificationDispatcher.Application.Dispatcher;

/// <summary>
/// Dispatches notifications concurrently with PriorityQueue, Retry and Timeout.
/// </summary>
public class NotificationDispatcher
{
    private readonly Dictionary<NotificationType, INotificationProvider> _providers;
    private readonly TimeSpan _perRequestTimeout;
    private readonly int _maxParallelism;
    private readonly int _maxRetry;

    /// <summary>
    /// Initializes the Dispatcher with the required settings.
    /// </summary>
    /// <param name="providers">Notification providers to use.</param>
    /// <param name="perRequestTimeout">Max time per notification before timeout.</param>
    /// <param name="maxParallelism">Max concurrent notifications.</param>
    /// <param name="maxRetry">Max retry attempts on failure.</param>
    public NotificationDispatcher(
        IEnumerable<INotificationProvider> providers,
        TimeSpan perRequestTimeout,
        int maxParallelism,
        int maxRetry)
    {
        _providers = providers.ToDictionary(p => p.Type);
        _perRequestTimeout = perRequestTimeout;
        _maxParallelism = maxParallelism;
        _maxRetry = maxRetry;
    }

    /// <summary>
    /// Dispatches a batch of notifications concurrently, prioritized by urgency.
    /// </summary>
    /// <param name="notifications">The list of notifications to dispatch.</param>
    /// <param name="ct">Cancellation token to cancel the entire operation.</param>
    public async Task DispatchAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct)
    {
        var priorityQueue = new PriorityQueue<Notification, int>();
        foreach (var notification in notifications)
            priorityQueue.Enqueue(notification, (int)notification.Priority);

        var sorted = new List<Notification>(priorityQueue.Count);
        while (priorityQueue.Count > 0)
            sorted.Add(priorityQueue.Dequeue());

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism };
        long successCount = 0, failureCount = 0, timeoutCount = 0;

        Console.WriteLine($"--- Starting dispatch for {sorted.Count} notifications ---");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        await Parallel.ForEachAsync(sorted, options, async (notification, _) =>
        {
            var result = await ProcessOneAsync(notification, ct);
            switch (result)
            {
                case Result.Success: Interlocked.Increment(ref successCount); break;
                case Result.Timeout: Interlocked.Increment(ref timeoutCount); break;
                default:             Interlocked.Increment(ref failureCount); break;
            }
        });

        watch.Stop();
        PrintSummary(watch.Elapsed.TotalSeconds, successCount, failureCount, timeoutCount);
    }

    /// <summary>
    /// Processes a single notification: find provider → send with retry → handle timeout.
    /// </summary>
    private async Task<Result> ProcessOneAsync(Notification notification, CancellationToken ct)
    {
        if (!_providers.TryGetValue(notification.Type, out var provider))
            return Result.Failure;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_perRequestTimeout);

        try
        {
            await Retry.ExecuteAsync(
                () => provider.SendAsync(notification, timeoutCts.Token),
                timeoutCts.Token,
                _maxRetry);
            return Result.Success;
        }
        catch (OperationCanceledException) { return Result.Timeout; }
        catch (NotificationDeliveryException) { return Result.Failure; }
    }

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

    private enum Result { Success, Failure, Timeout }
}
