using System.Diagnostics;
using System.Management;
using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Enforcement;
// Reactive classroom process filtering, NOT an OS security boundary. Windows
// applications and interpreters remain usable; process starts may race this watcher.
public sealed class AppAllowListEnforcer : IEnforcer,IDisposable
{
    public string Name=>"Student application filter (reactive)";
    private readonly ILogger _log;
    private readonly Func<IReadOnlyCollection<string>> _sids;
    private readonly Func<string,bool> _approved;
    private readonly object _gate=new();
    private ManagementEventWatcher? _watcher;
    private List<string> _rules=new();
    private string? _fault;
    public AppAllowListEnforcer(ILogger log,Func<IReadOnlyCollection<string>> sids,Func<string,bool> approved){_log=log;_sids=sids;_approved=approved;}
    public void Apply(EnforcementState s)
    {
        lock(_gate)
        {
            if(!s.AppAllowListEnabled || s.Overrides.GetValueOrDefault("apps")){Revert();return;}
            if(_sids().Count==0)throw new InvalidOperationException("No student accounts configured.");
            _rules=s.AllowedApps.ToList();
            if(_watcher is null)
            {
                _watcher=new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                _watcher.EventArrived+=(_,e)=>Check(Convert.ToInt32(e.NewEvent["ProcessID"]));
                _watcher.Stopped+=(_,e)=>{_fault="Application watcher stopped: "+e.Status;};
                try{_watcher.Start();_fault=null;}catch{_watcher.Dispose();_watcher=null;throw;}
            }
            if(_fault is not null){var error=_fault;Revert();throw new IOException(error);}
        }
        // Includes processes already open when a policy was enabled.
        foreach(var p in Process.GetProcesses()){using(p)Check(p.Id);}
    }
    private void Check(int pid)
    {
        lock(_gate)
        {
            if(_watcher is null)return;
            try
            {
                using var p=Process.GetProcessById(pid);
                if(!StudentProcess.IsStudent(p,_sids()))return;
                var path=p.MainModule?.FileName;
                if(path is null)return;
                var full=Path.GetFullPath(path);
                var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows)+Path.DirectorySeparatorChar;
                var own=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"ClassroomGuard")+Path.DirectorySeparatorChar;
                // Verify location; no exemption based on a spoofable system filename.
                if(full.StartsWith(windows,StringComparison.OrdinalIgnoreCase) || full.StartsWith(own,StringComparison.OrdinalIgnoreCase))return;
                // Learning UI requires the machine-wide WebView2 runtime. Match a protected
                // Microsoft runtime directory AND the executable, never filename alone.
                if(IsLearningRuntime(full))return;
                if(_rules.Any(rule=>Path.IsPathFullyQualified(rule)
                    ? StudentProcess.ProtectedPath(rule) && string.Equals(Path.GetFullPath(rule),full,StringComparison.OrdinalIgnoreCase)
                    : StudentProcess.ProtectedPath(full) && string.Equals(Path.GetFileName(full),rule,StringComparison.OrdinalIgnoreCase)))return;
                if(_approved(full))return;
                p.Kill(); // Do not kill system/teacher descendants as a process tree.
                _log.LogWarning("Filtered student process {Path}",full);
            }
            catch(ArgumentException){}catch(InvalidOperationException){}
            catch(Exception ex){_log.LogWarning(ex,"Could not inspect/filter student PID {Pid}",pid);}
        }
    }
    private static bool IsLearningRuntime(string full)
    {
        if(!string.Equals(Path.GetFileName(full),"msedgewebview2.exe",StringComparison.OrdinalIgnoreCase))return false;
        return new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}
            .Where(root=>!string.IsNullOrWhiteSpace(root))
            .Any(root=>full.StartsWith(Path.Combine(root,"Microsoft","EdgeWebView","Application")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
    }
    public void Revert(){lock(_gate){var w=_watcher;_watcher=null;if(w is not null){w.Stop();w.Dispose();}_fault=null;}}
    public void Dispose()=>Revert();
}
