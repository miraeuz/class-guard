using System.Threading;
using System.Windows;

namespace ClassroomGuard.StudentShell;

public partial class App : Application
{
    // Per-session (not per-machine) mutex: each Windows login is its own desktop
    // session, so the mutex name is scoped with the session id — otherwise a
    // teacher and a student sharing the machine at different times would fight
    // over a single global lock across logons.
    private static Mutex? _singleInstance;
    private static bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        var mutexName = $"Local\\ClassroomGuard.StudentShell.Session{sessionId}";
        _singleInstance = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        _ownsMutex=createdNew;
        if (!createdNew)
        {
            // Already running in this session (task fired twice, or the student
            // double-clicked a shortcut) — just exit quietly, no error dialog.
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if(_ownsMutex)_singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
