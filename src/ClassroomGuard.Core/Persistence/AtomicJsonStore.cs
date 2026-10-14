using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Core.Persistence;

/// <summary>
/// Durable JSON storage with ATOMIC writes (spec §1.5): we never edit a file in
/// place. We write to a temp file in the same directory, flush it, then move it
/// over the target. A power cut mid-write (common in school labs) therefore
/// leaves either the old complete file or the new complete file — never a
/// half-written one.
/// </summary>
public sealed class AtomicJsonStore<T> where T : class
{
    private readonly string _path;
    private readonly ILogger _log;
    private readonly Func<T> _failClosedFactory;
    private readonly object _gate = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public AtomicJsonStore(string path, Func<T> failClosedFactory, ILogger log)
    {
        _path = path;
        _failClosedFactory = failClosedFactory;
        _log = log;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    /// <summary>
    /// Load the persisted value. If the file is missing OR unreadable/corrupt,
    /// return the fail-closed default so enforcement stays ON (spec §1.3).
    /// </summary>
    public T Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    _log.LogInformation("State file not found; using fail-closed default.");
                    return _failClosedFactory();
                }

                var json = File.ReadAllText(_path);
                var value = JsonSerializer.Deserialize<T>(json, Options);
                if (value is null)
                {
                    _log.LogWarning("State file deserialized to null; using fail-closed default.");
                    return _failClosedFactory();
                }
                return value;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "State file corrupt/unreadable; using fail-closed default.");
                return _failClosedFactory();
            }
        }
    }

    /// <summary>Persist the value atomically.</summary>
    public void Save(T value)
    {
        lock (_gate)
        {
            var dir = Path.GetDirectoryName(_path)!;
            var tmp = Path.Combine(dir, $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                var json = JsonSerializer.Serialize(value, Options);
                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(json);
                    sw.Flush();
                    fs.Flush(true); // force to disk before the swap
                }

                // Atomic replace on the same volume.
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Atomic save failed for {Path}", _path);
                TryDelete(tmp);
                throw;
            }
        }
    }

    private static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
    }
}
