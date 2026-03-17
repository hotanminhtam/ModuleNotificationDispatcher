namespace ModuleNotificationDispatcher.Infrastructure.Resilience;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// Implements a simple Circuit Breaker pattern to protect against cascading failures.
/// </summary>
public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _recoveryTimeout;
    private int _failureCount = 0;
    private DateTime _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? recoveryTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _recoveryTimeout = recoveryTimeout ?? TimeSpan.FromSeconds(30);
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _lastFailureTime > _recoveryTimeout)
                {
                    return CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
            }
        }
    }

    public bool IsAllowed()
    {
        var state = State;
        return state == CircuitState.Closed || state == CircuitState.HalfOpen;
    }
}
