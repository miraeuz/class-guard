using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Core.Persistence;

/// <summary>Separate from policy synchronization and expiring restriction leases.</summary>
public sealed class ScreenLockStore
{
    public sealed class State { public bool Locked { get; set; } = true; }
    private readonly AtomicJsonStore<State> _store;
    public bool Locked { get; private set; }
    public ScreenLockStore(string path, ILogger log)
    {
        _store = new(path, () => new State(), log);
        // A first install starts unlocked. Existing unreadable state fails closed.
        if (!File.Exists(path)) _store.Save(new State { Locked = false });
        Locked = _store.Load().Locked;
    }
    public void Set(bool locked)
    {
        _store.Save(new State { Locked = locked });
        Locked = locked; // acknowledge only after durable write succeeds
    }
}
