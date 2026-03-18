namespace ModuleNotificationDispatcher.Infrastructure.Resilience;

/// <summary>
/// Retry: Thử lại một hành động nếu bị lỗi.
/// Sử dụng Exponential Backoff: thời gian chờ tăng dần giữa các lần retry.
///
/// Ví dụ với maxRetry = 3:
///   Lần 1 lỗi → chờ 500ms  → thử lần 2
///   Lần 2 lỗi → chờ 1000ms → thử lần 3
///   Lần 3 lỗi → chờ 2000ms → thử lần 4 (lần cuối)
///   Lần 4 lỗi → THROW EXCEPTION (hết số lần retry)
/// </summary>
public static class Retry
{
    /// <summary>
    /// Thực thi một action bất đồng bộ với cơ chế retry.
    /// </summary>
    /// <param name="action">Hành động cần thực hiện (ví dụ: gửi email).</param>
    /// <param name="cancellationToken">Token để hủy nếu bị timeout hoặc Ctrl+C.</param>
    /// <param name="maxRetry">Số lần thử lại tối đa (mặc định 3).</param>
    public static async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        int maxRetry = 3)
    {
        int retryCount = 0;

        while (true)
        {
            // Kiểm tra xem có bị hủy chưa (timeout hoặc Ctrl+C)
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Thử thực hiện action
                await action();
                return; // Thành công → thoát vòng lặp
            }
            catch (OperationCanceledException)
            {
                // Bị hủy (timeout hoặc Ctrl+C) → không retry, ném lỗi lên
                throw;
            }
            catch (Exception)
            {
                // Lỗi khác → kiểm tra còn lần retry không
                if (retryCount >= maxRetry)
                    throw; // Hết lần retry → ném lỗi lên

                // Tính thời gian chờ theo Exponential Backoff:
                // retryCount=0 → 500ms, retryCount=1 → 1000ms, retryCount=2 → 2000ms
                int delay = (int)Math.Pow(2, retryCount) * 500;
                retryCount++;

                // Chờ trước khi thử lại (có thể bị hủy bởi cancellationToken)
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
