namespace ModuleNotificationDispatcher.Infrastructure.Resilience;

/// <summary>
/// Retry: Re-executes an action if it fails.
/// Uses Exponential Backoff: wait time increases between each retry attempt.
///
/// Example with maxRetry = 3:
///   Attempt 1 fails → wait 500ms  → Attempt 2
///   Attempt 2 fails → wait 1000ms → Attempt 3
///   Attempt 3 fails → wait 2000ms → Attempt 4 (final)
///   Attempt 4 fails → THROW EXCEPTION (retries exhausted)
/// </summary>
public static class Retry
{
    /// <summary>
    /// Executes an async action with retry logic.
    /// </summary>
    /// <param name="action">The action to perform (e.g., send an email).</param>
    /// <param name="cancellationToken">Token to cancel if timeout occurs or Ctrl+C is pressed.</param>
    /// <param name="maxRetry">Maximum number of retry attempts (default: 3).</param>
    public static async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        int maxRetry = 3)
    {
        int retryCount = 0;

        while (true)
        {
            // Check if cancellation has been requested (timeout or Ctrl+C)
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Try to execute the action
                await action();
                return; // Success → exit the loop
            }
            catch (OperationCanceledException)
            {
                // Cancelled (timeout or Ctrl+C) → do not retry, propagate immediately
                throw;
            }
            catch (Exception)
            {
                // Other error → check if retries remain
                if (retryCount >= maxRetry)
                    throw; // No retries left → propagate the error

                // Calculate Exponential Backoff delay:
                // retryCount=0 → 500ms, retryCount=1 → 1000ms, retryCount=2 → 2000ms
                int delay = (int)Math.Pow(2, retryCount) * 500;
                retryCount++;

                // Wait before next attempt (can be cancelled by cancellationToken)
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
