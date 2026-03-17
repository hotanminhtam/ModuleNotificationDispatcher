using ModuleNotificationDispatcher.Infrastructure.Resilience;
using ModuleNotificationDispatcher.Domain.Models;
using ModuleNotificationDispatcher.Infrastructure.Providers;
using ModuleNotificationDispatcher.Application.Validation;
using ModuleNotificationDispatcher.Domain.Interfaces;

namespace ModuleNotificationDispatcher.Application.Dispatcher;

/// <summary>
/// Handles the concurrent dispatching of notifications across different providers (Email, Sms, etc.)
/// </summary>
public class NotificationDispatcher
{
    private readonly Dictionary<NotificationType, INotificationProvider> _providers;
    private readonly Dictionary<NotificationType, CircuitBreaker> _circuitBreakers;
    private readonly TimeSpan _perRequestTimeout;
    private readonly int _maxParallelism;
    private readonly int _maxRetry;

    /// <summary>
    /// Initializes a new instance of the NotificationDispatcher.
    /// </summary>
    /// <param name="providers">The notification providers available (default: Email and SMS).</param>
    /// <param name="perRequestTimeout">Maximum time allowed for a single notification attempt.</param>
    /// <param name="maxParallelism">Maximum number of concurrent processing tasks.</param>
    /// <param name="maxRetry">Maximum number of retry attempts for failed deliveries.</param>
    public NotificationDispatcher(
        IEnumerable<INotificationProvider>? providers = null,
        TimeSpan? perRequestTimeout = null,
        int maxParallelism = 5000,
        int maxRetry = 3,
        int cbFailureThreshold = 10,
        TimeSpan? cbRecoveryTimeout = null)
    {
        providers ??= [new EmailNotificationProvider(), new SmsNotificationProvider(), new PushNotificationProvider()];
        _providers = providers.ToDictionary(p => p.Type);
        _circuitBreakers = _providers.Keys.ToDictionary(type => type, _ => new CircuitBreaker(cbFailureThreshold, cbRecoveryTimeout));
        
        _perRequestTimeout = perRequestTimeout ?? TimeSpan.FromSeconds(30);
        _maxParallelism = maxParallelism;
        _maxRetry = maxRetry;
    }

    /// <summary>
    /// Dispatches a batch of notifications concurrently with priority sorting.
    /// </summary>
    /// <param name="notifications">The list of notifications to send.</param>
    /// <param name="ct">Cancellation token for the entire operation.</param>
    public async Task DispatchAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct)
    {
        // 1. Sort by Priority: High(1) -> Medium(2) -> Low(3)
        var sortedNotifications = notifications.OrderBy(n => (int)n.Priority);

        var parallelOptions = new ParallelOptions
        {
            // Set maximum degree of parallelism based on configuration
            MaxDegreeOfParallelism = _maxParallelism, 
        };

        long successCount = 0, failureCount = 0, timeoutCount = 0;

        Console.WriteLine($"--- Starting dispatch for {notifications.Count()} notifications ---");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // 2. Process notifications in parallel while ensuring high throughput
        await Parallel.ForEachAsync(sortedNotifications, parallelOptions, async (notification, _) =>
        {
            var result = await ProcessNotificationAsync(notification, ct);
            
            switch (result)
            {
                case ProcessingResult.Success: Interlocked.Increment(ref successCount); break;
                case ProcessingResult.Failure: Interlocked.Increment(ref failureCount); break;
                case ProcessingResult.Timeout: Interlocked.Increment(ref timeoutCount); break;
                case ProcessingResult.Invalid: 
                    Console.WriteLine($"[VALIDATION ERROR] {notification.Id} - Invalid data");
                    Interlocked.Increment(ref failureCount); 
                    break;
                case ProcessingResult.ServiceUnavailable:
                    Console.WriteLine($"[CIRCUIT OPEN] {notification.Type} is temporarily unavailable.");
                    Interlocked.Increment(ref failureCount);
                    break;
            }
        });

        watch.Stop();
        PrintSummary(watch.Elapsed.TotalSeconds, successCount, failureCount, timeoutCount);
    }

    /// <summary>
    /// Processes a single notification including validation, retry logic and timeout enforcement.
    /// </summary>
    private async Task<ProcessingResult> ProcessNotificationAsync(
        Notification notification,
        CancellationToken ct)
    {
        // 1. Validation
        var validation = NotificationValidator.Validate(notification);
        if (!validation.IsValid) return ProcessingResult.Invalid;

        // 2. Circuit Breaker Check
        if (!_providers.TryGetValue(notification.Type, out var provider) || 
            !_circuitBreakers.TryGetValue(notification.Type, out var cb))
            return ProcessingResult.Failure;

        if (!cb.IsAllowed()) return ProcessingResult.ServiceUnavailable;

        // 3. Create a linked cancellation token to enforce per-request timeout
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(_perRequestTimeout);

        try
        {
            await Retry.ExecuteAsync(async () =>
            {
                await provider.SendAsync(notification, linkedCts.Token);
                cb.RecordSuccess();
            }, linkedCts.Token, _maxRetry);

            return ProcessingResult.Success;
        }
        catch (OperationCanceledException)
        {
            cb.RecordFailure();
            return ProcessingResult.Timeout;
        }
        catch
        {
            cb.RecordFailure();
            return ProcessingResult.Failure;
        }
    }

    private static void PrintSummary(double totalSeconds, long success, long failure, long timeout)
    {
        Console.WriteLine("\n==========================================");
        Console.WriteLine("NOTIFICATION DISPATCH SUMMARY");
        Console.WriteLine($"Total Time: {totalSeconds:F2}s");
        Console.WriteLine($"Success:    {success}");
        Console.WriteLine($"Failure:    {failure}");
        Console.WriteLine($"Timeout:    {timeout}");
        Console.WriteLine("==========================================\n");
    }

    private enum ProcessingResult { Success, Failure, Timeout, Invalid, ServiceUnavailable }
}
