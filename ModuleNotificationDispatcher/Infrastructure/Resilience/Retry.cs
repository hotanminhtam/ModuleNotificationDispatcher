namespace ModuleNotificationDispatcher.Infrastructure.Resilience;

/// <summary>
/// Provides resilient execution logic for asynchronous operations.
/// </summary>
public static class Retry
{
    /// <summary>
    /// Executes an asynchronous action with a retry policy based on exponential backoff.
    /// </summary>
    /// <param name="action">The asynchronous task to perform.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation or timeout.</param>
    /// <param name="maxRetry">Maximum number of retry attempts (default is 3).</param>
    public static async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        int maxRetry = 3)
    {
        int retryCount = 0;
        while (true)
        {
            // Fail fast if cancellation has already been requested
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action();
                return;
            }
            catch (OperationCanceledException)
            {
                // Do not retry on cancellation or explicit timeout; propagating upward.
                throw;
            }
            catch (Exception)
            {
                // If max retries reached, propagate the error.
                if (retryCount >= maxRetry)
                    throw;

                // Calculate exponential backoff delay (e.g., 500ms, 1000ms, 2000ms)
                const int BaseDelayMilliseconds = 500;
                int delay = (int)Math.Pow(2, retryCount) * BaseDelayMilliseconds;
                retryCount++;

                // Await a cancellable delay before the next attempt.
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
