namespace ClassroomGuard.Core.Security;

/// <summary>
/// Sliding-window failed-attempt limiter. Both the offline TOTP unlock code and
/// the Telegram install-approval code are 6 digits (1,000,000 combinations) and,
/// unlike the teacher password, are verified with cheap hashing (HMAC-SHA1 /
/// dictionary lookup) rather than PBKDF2 — so without this, a local process could
/// simply iterate the whole keyspace over the named pipe fast enough to matter
/// (no network latency, no OS-level throttling). This makes that impractical:
/// once the failure cap is hit within the window, further attempts are rejected
/// instantly (not even checked) until the window rolls forward.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _failures = new();
    private readonly int _maxFailures;
    private readonly TimeSpan _window;

    public RateLimiter(int maxFailuresPerWindow, TimeSpan window)
    {
        _maxFailures = maxFailuresPerWindow;
        _window = window;
    }

    /// <summary>True if too many failures have happened recently — caller should refuse
    /// outright without even checking the supplied code/password.</summary>
    public bool IsBlocked()
    {
        lock (_gate)
        {
            Prune();
            return _failures.Count >= _maxFailures;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            Prune();
            _failures.Enqueue(DateTimeOffset.UtcNow);
        }
    }

    /// <summary>A real success clears the window — this throttles brute force, not the
    /// legitimate teacher who fat-fingered a digit once or twice.</summary>
    public void RecordSuccess()
    {
        lock (_gate) { _failures.Clear(); }
    }

    private void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow - _window;
        while (_failures.Count > 0 && _failures.Peek() < cutoff)
            _failures.Dequeue();
    }
}
