using System.Security.Cryptography;

namespace ClassroomGuard.Core.Security;

/// <summary>Bounded-time, single-use in-memory approval codes for exact file hashes. No SRP exception is generated.</summary>
public sealed class InstallCodeStore
{
    public sealed record PendingInstall(
        string Code,
        string FilePath,
        string Sha256Hex,
        DateTimeOffset ExpiresAt);

    private readonly object _gate = new();
    private readonly Dictionary<string, PendingInstall> _byCode = new();
    private readonly TimeSpan _ttl;
    private readonly RateLimiter _throttle = new(maxFailuresPerWindow: 8, window: TimeSpan.FromMinutes(1));

    public InstallCodeStore(TimeSpan? ttl = null) => _ttl = ttl ?? TimeSpan.FromMinutes(10);

    /// <summary>Issue a new code for a blocked file. Never collides with a still-live code.</summary>
    public PendingInstall Issue(string filePath, string sha256Hex)
    {
        lock (_gate)
        {
            Prune();
            string code;
            do { code = GenerateCode(); } while (_byCode.ContainsKey(code));

            var pending = new PendingInstall(code, filePath, sha256Hex, DateTimeOffset.UtcNow + _ttl);
            _byCode[code] = pending;
            return pending;
        }
    }

    /// <summary>
    /// Validate and consume a code (single use). Returns the matching pending
    /// install, or null if the code is wrong, expired, or already used.
    /// </summary>
    public PendingInstall? Redeem(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();

        lock (_gate)
        {
            if (_throttle.IsBlocked()) return null; // too many wrong guesses recently — don't even look

            Prune();
            if (!_byCode.TryGetValue(code, out var pending))
            {
                _throttle.RecordFailure();
                return null;
            }
            _byCode.Remove(code); // one-time use regardless of outcome

            if (pending.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _throttle.RecordFailure();
                return null;
            }
            _throttle.RecordSuccess();
            return pending;
        }
    }

    private void Prune()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _byCode.Where(kv => kv.Value.ExpiresAt < now).Select(kv => kv.Key).ToList())
            _byCode.Remove(key);
    }

    private static string GenerateCode()
    {
        // 6-digit, CSPRNG, zero-padded — same shape as the offline TOTP unlock code
        // so the StudentShell UI can reuse one "6-digit code" mental model.
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D6");
    }
}
