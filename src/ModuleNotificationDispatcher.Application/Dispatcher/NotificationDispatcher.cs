using Microsoft.Extensions.Logging;
using ModuleNotificationDispatcher.Domain.Exceptions;
using ModuleNotificationDispatcher.Domain.Interfaces;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Application.Resilience;

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
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>
    /// Initializes the Dispatcher via DI.
    /// </summary>
    public NotificationDispatcher(
        IEnumerable<INotificationProvider> providers,
        DispatcherSettings settings,
        ILogger<NotificationDispatcher> logger)
    {
        _providers = providers.ToDictionary(p => p.Type);
        _perRequestTimeout = TimeSpan.FromSeconds(settings.PerRequestTimeoutSeconds);
        _maxParallelism = settings.MaxParallelism;
        _maxRetry = settings.MaxRetry;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a batch of notifications concurrently, prioritized by urgency.
    /// </summary>
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

        _logger.LogInformation("Starting dispatch for {Count} notifications", sorted.Count);
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
        _logger.LogInformation(
            "Dispatch complete — {Elapsed:F2}s | Success: {Success} | Failure: {Failure} | Timeout: {Timeout}",
            watch.Elapsed.TotalSeconds, successCount, failureCount, timeoutCount);
    }

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

    private enum Result { Success, Failure, Timeout }
}
