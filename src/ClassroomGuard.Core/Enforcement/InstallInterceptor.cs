using System.Management;
using System.Security.Cryptography;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Core.Enforcement;

/// <summary>Reactive installer interception for enrolled student accounts. Exact-file approvals are shared with the app filter. This is not pre-execution prevention.</summary>
public sealed class InstallInterceptor : IEnforcer, IDisposable
{
    public string Name => "Install(Interceptor)";

    private static readonly string[] InstallerExtensions = { ".exe", ".msi" };

    private static readonly string[] SuspectPathHints =
    {
        "\\downloads\\", "\\desktop\\", "\\temp\\", "\\appdata\\local\\temp\\",
    };

    private readonly ILogger _log;
    private readonly Func<IReadOnlyCollection<string>> _sids;
    private string? _fault;
    private readonly Dictionary<string,DateTimeOffset> _notified = new();
    private readonly InstallCodeStore _codes;
    private readonly TelegramNotifier _telegram;
    private readonly Dictionary<string, DateTimeOffset> _tempAllowed = new(); // sha256hex -> expiry
    private readonly object _gate = new();

    private ManagementEventWatcher? _watcher;
    private bool _active;

    public InstallInterceptor(ILogger log, InstallCodeStore codes, TelegramNotifier telegram, Func<IReadOnlyCollection<string>> sids)
    {
        _log = log; _sids = sids;
        _codes = codes;
        _telegram = telegram;
    }

    public void Apply(EnforcementState state)
    {
        if (_fault is not null) { var error=_fault; Stop(); _fault=null; throw new IOException(error); }
        var shouldBlock = state.InstallBlocking;
        if (state.Overrides.TryGetValue("install", out var allow) && allow)
            shouldBlock = false;

        if (shouldBlock && _sids().Count==0) throw new InvalidOperationException("No student accounts configured.");
        if (shouldBlock && !_active) Start();
        else if (!shouldBlock && _active) Stop();
    }

    public void Revert() => Stop();

    /// <summary>Called from the Agent when StudentShell forwards an ApproveInstall request.</summary>
    public (bool ok, string message) ApproveByCode(string code)
    {
        var pending = _codes.Redeem(code);
        if (pending is null)
            return (false, "Kod noto'g'ri yoki muddati o'tgan. Ustozdan yangi kod so'rang.");

        lock (_gate)
        {
            _tempAllowed[pending.Sha256Hex] = DateTimeOffset.UtcNow.AddMinutes(15);
        }

        var fileName = Path.GetFileName(pending.FilePath);
        _log.LogWarning("[Install] temporary approval granted for {File}.", pending.FilePath);
        return (true, $"Ruxsat berildi: {fileName}. Endi uni qayta ishga tushiring (15 daqiqa amal qiladi).");
    }

    public bool IsApproved(string path)
    {
        lock(_gate)
        {
            var now=DateTimeOffset.UtcNow;
            foreach(var k in _tempAllowed.Where(x=>x.Value<=now).Select(x=>x.Key).ToList())_tempAllowed.Remove(k);
            if(_tempAllowed.Count==0)return false;
        }
        var hash=TrySha256(path);
        lock(_gate)return hash is not null && _tempAllowed.TryGetValue(hash,out var until) && until>DateTimeOffset.UtcNow;
    }

    private void Start()
    {
        try
        {
            _watcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _watcher.EventArrived += OnProcessStart;
            _watcher.Stopped += (_, e) => { if(_active) _fault = "Installer watcher stopped: " + e.Status; };
            _watcher.Start();
            _active = true;
            _log.LogInformation("[Install] interceptor armed (process-start watch).");
        }
        catch (Exception ex)
        {
            // Common cause: WMI trace provider unavailable / access denied — should not
            // happen when running as LocalSystem, but never crash the Agent over this.
            _log.LogError(ex, "[Install] interceptor failed to start; installs are NOT blocked.");
            _active = false;
            _watcher?.Dispose(); _watcher=null;
            throw;
        }
    }

    private void Stop()
    {
        ManagementEventWatcher? watcher;
        lock(_gate){_active=false;watcher=_watcher;_watcher=null;_fault=null;}
        if(watcher is not null)
        {
            watcher.EventArrived-=OnProcessStart;
            try{watcher.Stop();}finally{watcher.Dispose();}
        }
    }

    private void OnProcessStart(object sender, EventArrivedEventArgs e)
    {
        lock(_gate)
        {
        if(!_active)return;
        try
        {
            var pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"]?.Value ?? 0);
            if (pid <= 0) return;

            var path = TryResolveImagePath(pid);
            if (string.IsNullOrEmpty(path) || !LooksLikeInstaller(path)) return;

            using var process = System.Diagnostics.Process.GetProcessById(pid);
            if (!StudentProcess.IsStudent(process, _sids())) return;
            if (IsApproved(path)) return;
            process.Kill(); // failure propagates; never report a failed kill as blocked
            if (!process.WaitForExit(2000)) throw new IOException("Installer did not terminate.");
            var hash = TrySha256(path);

            if (hash is null)
            {
                _log.LogWarning("[Install] blocked {File} (killed) but could not hash it — no code issued.", path);
                return;
            }

            lock(_gate)
            {
                var now=DateTimeOffset.UtcNow;
                foreach(var k in _notified.Where(x=>now-x.Value>TimeSpan.FromMinutes(10)).Select(x=>x.Key).ToList())_notified.Remove(k);
                if(_notified.TryGetValue(hash,out var last) && now-last<TimeSpan.FromMinutes(1))return;
                if(_notified.Count>=20)return;
                _notified[hash]=now;
            }
            var pending = _codes.Issue(path, hash);
            _log.LogWarning("[Install] blocked {File}; approval issued.", path);

            var fileName = Path.GetFileName(path);
            _ = _telegram.SendAsync(
                $"🔒 ClassroomGuard: o'rnatish bloklandi\n" +
                $"Kompyuter: {MachineIdentity.Name}\n" +
                $"Fayl: {fileName}\n" +
                $"Yo'l: {path}\n" +
                $"Ruxsat kodi: {pending.Code} (10 daqiqa amal qiladi)\n" +
                $"Kodni o'quvchiga ayting — StudentShell'da kiritadi.");
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "[Install] event handling failed (non-fatal).");
        }
    }
    }

    private static string? TryResolveImagePath(int pid)
    {
        // Win32_ProcessStartTrace only gives a bare process name; look up the full
        // path via Win32_Process, falling back to the .NET Process API.
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (ManagementObject mo in searcher.Get())
            {
                var p = mo["ExecutablePath"] as string;
                if (!string.IsNullOrEmpty(p)) return p;
            }
        }
        catch { /* fall through */ }

        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(pid);
            return proc.MainModule?.FileName;
        }
        catch
        {
            return null; // process already exited, or access denied (elevated installer) — can't resolve
        }
    }

    private static bool LooksLikeInstaller(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!InstallerExtensions.Contains(ext)) return false;

        // Never touch anything under Windows or Program Files — that's normal
        // OS/app activity, not a student-run installer.
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var lower = path.ToLowerInvariant();
        if (StartsWithPath(lower, windir) || StartsWithPath(lower, pf) || StartsWithPath(lower, pfx86))
            return false;

        if (SuspectPathHints.Any(hint => lower.Contains(hint))) return true;

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.DriveType == DriveType.Removable) return true;
            }
        }
        catch { /* unknown drive type — don't block on this signal alone */ }

        return false;
    }

    private static bool StartsWithPath(string lowerPath, string? folder) =>
        !string.IsNullOrEmpty(folder) && lowerPath.StartsWith(Path.TrimEndingDirectorySeparator(folder).ToLowerInvariant() + Path.DirectorySeparatorChar);

    private static void TryKill(int pid, string path)
    {
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(pid);
            proc.Kill(entireProcessTree: true);
        }
        catch { /* already exited, or couldn't open handle — nothing more we can do */ }
    }

    private static string? TrySha256(string path)
    {
        try
        {
            // Give the OS a brief moment to release the file handle post-kill.
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    using var sha = SHA256.Create();
                    return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
                }
                catch (IOException) { Thread.Sleep(100); }
            }
            return null;
        }
        catch
        {
            return null; // file gone, no permission, etc. — skip hashing, still counts as blocked
        }
    }

    public void Dispose() => Stop();
}
