namespace ModuleNotificationDispatcher.Application.Resilience;

/// <summary>
/// Retries an async action with Exponential Backoff (500ms → 1s → 2s → ...).
/// </summary>
public static class Retry
{
    /// <summary>
    /// Executes an async action with retry logic.
    /// </summary>
    /// <param name="action">The action to perform.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="maxRetry">Maximum retry attempts (default: 3).</param>
    public static async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        int maxRetry = 3)
    {
        int retryCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action();
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                if (retryCount >= maxRetry)
                    throw;

                int delay = (int)Math.Pow(2, retryCount) * 500;
                retryCount++;
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
