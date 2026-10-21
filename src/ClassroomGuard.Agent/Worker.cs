using System.Runtime.InteropServices;
using ClassroomGuard.Agent.Net;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Enforcement;
using ClassroomGuard.Core.Ipc;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Persistence;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Agent;
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly AgentConfig _config;
    private readonly PolicyStore _store;
    private readonly ScreenLockStore _screenLock;
    private readonly LessonStore _lessons;
    private DateTimeOffset? _helpAt;
    private int _helpSession=-1;
    private string _helpUser="";
    private readonly Dictionary<string,DateTimeOffset> _helpCooldown=new();
    private readonly ScreenCoverTracker _screenCover=new();
    private readonly CredentialStore _credentials;
    private readonly TelegramNotifier _telegram;
    private readonly InstallInterceptor _installer;
    private readonly EnforcementCoordinator _coordinator;
    private readonly AttemptStore _attempts;
    private readonly ClassroomKey _key;
    private readonly object _gate=new();
    private readonly Dictionary<string,RateLimiter> _throttles=new();
    private readonly Dictionary<string,DateTimeOffset> _usedCodes=new();
    private EnforcementState _baseline;
    private AgentHealth _health=new();
    private DateTimeOffset? _unlockUntil;
    private int _leaseSession=-1;
    private string _leaseUser="";
    private DateTimeOffset? _preparedUntil;
    [DllImport("Wtsapi32.dll",CharSet=CharSet.Unicode)]private static extern bool WTSQuerySessionInformation(IntPtr server,int session,int info,out IntPtr buffer,out int bytes);
    [DllImport("Wtsapi32.dll")]private static extern void WTSFreeMemory(IntPtr pointer);
    [DllImport("kernel32.dll")]private static extern uint WTSGetActiveConsoleSessionId();
    private static string SessionUser(int id)
    {
        if(id<0 || !WTSQuerySessionInformation(IntPtr.Zero,id,5,out var p,out _))return "";
        try{return Marshal.PtrToStringUni(p)??"";}finally{WTSFreeMemory(p);}
    }
    public Worker(ILogger<Worker> log)
    {
        _log=log;_config=AgentConfig.Load(log);
        if(_config.CommandPort is <1024 or >65535 || _config.AnnouncePort is <1024 or >65535)throw new InvalidDataException("Invalid configured port.");
        _lessons=new(Path.Combine(AppPaths.DataDir,"lesson.json"),log);
        _screenLock=new(Path.Combine(AppPaths.DataDir,"screen-lock.json"),log);
        _store=new(AppPaths.StateFile,log);_baseline=_store.Load();
        _credentials=new(AppPaths.UsersFile,AppPaths.BootstrapFile,log);
        _key=ClassroomKey.FromBase64(File.ReadAllText(AppPaths.ClassroomKeyFile).Trim());
        _telegram=new(log,()=>_config.TelegramBotToken,()=>_config.TelegramChatId);
        _installer=new(log,new InstallCodeStore(),_telegram,()=>_config.StudentUserSids);
        _coordinator=new(log,()=>_config.EnableSrpEnforcement,new IEnforcer[]{new HostsEnforcer(log),new SrpInstallEnforcer(log,()=>_config.EnableSrpEnforcement),new DownloadEnforcer(log),_installer,new AppAllowListEnforcer(log,()=>_config.StudentUserSids,_installer.IsApproved)});
        _attempts=new(log);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _credentials.EnsureBootstrapUser();
        lock(_gate)Reconcile();
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var ct=linked.Token;
        var tasks=new[]{new ControlPipeServer(_log,HandleLocal).RunAsync(ct),new TlsCommandServer(_log,_config.CommandPort,_key,HandleNet).RunAsync(ct),new UdpAnnouncer(_log,_config.AnnouncePort,_config.CommandPort,()=>Effective().StudentModeActive).RunAsync(ct),SuperviseAsync(ct)};
        var first=await Task.WhenAny(tasks);
        if(!stoppingToken.IsCancellationRequested)Environment.ExitCode=1;
        linked.Cancel();
        try{await Task.WhenAll(tasks);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
        if(!stoppingToken.IsCancellationRequested){Environment.ExitCode=1;throw new IOException("Agent subsystem stopped unexpectedly.");}
    }
    private async Task SuperviseAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5),ct);
            lock(_gate)Reconcile();
            try{await _attempts.DeliverAsync(_telegram,ct);}catch(Exception ex)when(!ct.IsCancellationRequested){_log.LogError(ex,"Result delivery/storage failure");}
        }
    }
    private EnforcementState Effective()
    {
        lock(_gate)
        {
            if(_unlockUntil is {} until && (until<=DateTimeOffset.UtcNow || (_leaseSession>=0 && SessionUser(_leaseSession)!=_leaseUser)))_unlockUntil=null;
            var s=(_lessons.Active?.Policy??_baseline).Clone();s.StudentModeActive=!(_unlockUntil>DateTimeOffset.UtcNow || _preparedUntil>DateTimeOffset.UtcNow);return s;
        }
    }
    private void Reconcile()
    {
        _health=_coordinator.ApplyStudentModeOrRelease(Effective());_health.UnlockUntil=_unlockUntil;_health.ScreenLocked=_screenLock.Locked;
    }
    private void Unlock(int session)
    {
        _leaseSession=session>=0?session:unchecked((int)WTSGetActiveConsoleSessionId());
        _leaseUser=SessionUser(_leaseSession);_unlockUntil=DateTimeOffset.UtcNow.AddMinutes(30);_preparedUntil=null;
    }
    private bool Authorized(NetRequest r,bool management,LocalCaller? caller,out string why)
    {
        why="";if(management || caller?.Administrator==true)return true;
        var identity=caller?.Sid??"unknown";
        if(!_throttles.TryGetValue(identity,out var limiter))_throttles[identity]=limiter=new(8,TimeSpan.FromMinutes(1));
        if(limiter.IsBlocked()){why="Too many attempts. Retry after one minute, or use the elevated recovery tool.";return false;}
        foreach(var c in _usedCodes.Where(x=>x.Value<DateTimeOffset.UtcNow).Select(x=>x.Key).ToList())_usedCodes.Remove(c);
        if(!string.IsNullOrWhiteSpace(r.Code) && !_usedCodes.ContainsKey(r.Code.Trim()) && Totp.Verify(_key.TotpSeed(MachineIdentity.MachineId),r.Code))
        {_usedCodes[r.Code.Trim()]=DateTimeOffset.UtcNow.AddMinutes(2);limiter.RecordSuccess();return true;}
        if(!string.IsNullOrEmpty(r.Username) && !string.IsNullOrEmpty(r.Password) && r.Password.Length<=256 && _credentials.Verify(r.Username,r.Password,out var change))
        {
            if(change){why="Change bootstrap password first. New password must contain at least 12 characters.";return false;}
            limiter.RecordSuccess();return true;
        }
        limiter.RecordFailure();why="Invalid credentials or expired/used code.";return false;
    }
    private NetResponse Apply(NetRequest r,bool management,LocalCaller? caller=null)
    {
        lock(_gate)
        {
            if(r.Command==NetCommand.GetStatus)return Status();
            if(r.Command==NetCommand.Ping)return NetResponse.Success("pong");
            if(r.Command!=NetCommand.EnableStudent && !Authorized(r,management,caller,out var why))return NetResponse.Fail(why);
            switch(r.Command)
            {
                case NetCommand.BeginLesson:
                    _lessons.Begin(r.Lesson??throw new ArgumentException("Lesson required."));_unlockUntil=null;_preparedUntil=null;break;
                case NetCommand.EndLesson:
                    var ending=_lessons.Active;
                    _lessons.End(r.LessonId??"");_unlockUntil=null;_preparedUntil=null;Reconcile();
                    var ended=Status();ended.Ok=_health.Healthy;ended.Message="Lesson ended; baseline restored. Screen lock and exam pause unchanged.";
                    if(ending is not null)ended.LessonSummary=_attempts.Summarize(ending);
                    return ended;
                case NetCommand.AcknowledgeHelp:
                    _helpAt=null;return Status();
                case NetCommand.PauseTests:
                case NetCommand.ResumeTests:
                    var affected=_attempts.SetPaused(r.Command==NetCommand.PauseTests,management?"authenticated classroom controller":caller?.Sid??"local teacher");
                    var timing=Status();timing.Message=$"{affected} active attempts changed; future attempts unaffected.";return timing;
                case NetCommand.LockScreen:
                case NetCommand.UnlockScreen:
                    _screenLock.Set(r.Command==NetCommand.LockScreen);_screenCover.Reset();
                    _health.ScreenLocked=_screenLock.Locked;
                    var lockedResponse=Status();
                    lockedResponse.Message=_screenLock.Locked?"Screen lock saved; awaiting student shell display.":"Screen lock released; app restrictions unchanged.";
                    return lockedResponse;
                case NetCommand.EnableStudent:_unlockUntil=null;_preparedUntil=null;break;
                case NetCommand.DisableStudent:Unlock(caller?.SessionId??-1);break;
                case NetCommand.SetState:
                    if(_lessons.Active is not null)return NetResponse.Fail("End the active lesson before replacing the baseline.");
                    if(r.State is null)return NetResponse.Fail("No policy supplied.");
                    PolicyValidator.Validate(r.State);
                    var candidate=r.State.Clone();_store.Save(candidate);_baseline=candidate;_unlockUntil=null;_preparedUntil=null;break;
                case NetCommand.Uninstall:
                    var restored=_coordinator.RevertAll();_health=restored;
                    if(!restored.Healthy)return new(){Ok=false,Message="Recovery incomplete. Keep files and backup; inspect control errors.",Health=restored};
                    _screenLock.Set(false);_health.ScreenLocked=false;
                    _preparedUntil=DateTimeOffset.UtcNow.AddMinutes(5);
                    return new(){Ok=true,Message="Restrictions restored. Stop/remove service within 5 minutes; a restart resumes protection.",Health=restored};
                default:return NetResponse.Fail("Unsupported command.");
            }
            Reconcile();var response=Status();response.Ok=_health.Healthy;response.Message=_health.Healthy?"Applied and verified; reactive controls are not a security sandbox.":"Partial failure: inspect control health.";return response;
        }
    }
    private NetResponse Status()
    {
        _health.LessonId=_lessons.Active?.Id;_health.LessonTitle=_lessons.Active?.Title;_health.LessonGroup=_lessons.Active?.Group;
        _health.PausedTests=_attempts.PausedCount;
        var activeSession=unchecked((int)WTSGetActiveConsoleSessionId());
        _health.HelpRequested=_helpAt is not null && _helpSession==activeSession && SessionUser(activeSession)==_helpUser;
        _health.HelpRequestedAt=_health.HelpRequested?_helpAt:null;
        _health.ScreenLockToken=_screenCover.Token;
        _health.ScreenCoverConfirmed=_screenLock.Locked && _screenCover.IsFresh(unchecked((int)WTSGetActiveConsoleSessionId()),DateTimeOffset.UtcNow);
        _health.ScreenCoverReportedAt=_screenCover.ReportedAt;
        _health.ScreenCoverAgeSeconds=_screenCover.ReportedAt is {} reported?Math.Max(0,(DateTimeOffset.UtcNow-reported).TotalSeconds):8;
        _health.ScreenMonitorCount=_health.ScreenCoverConfirmed?_screenCover.MonitorCount:0;
        return new(){Ok=true,Message=_health.Summary,State=Effective(),Health=_health,Machine=new(){MachineId=MachineIdentity.MachineId,Name=MachineIdentity.Name,Ip=MachineIdentity.LocalIPv4(),Port=_config.CommandPort,StudentMode=Effective().StudentModeActive,Version=MachineIdentity.Version,LastSeen=DateTimeOffset.UtcNow}};
    }
    private ControlResponse HandleLocal(ControlRequest r,LocalCaller caller)
    {
        lock(_gate)
        {
            if(r.Action is ControlAction.RaiseHand or ControlAction.LowerHand)
            {
                if((!_config.StudentUserSids.Contains(caller.Sid)&&!caller.Administrator)||caller.SessionId!=unchecked((int)WTSGetActiveConsoleSessionId()))return ControlResponse.Fail("Only the active enrolled console user may request help.");
                if(r.Action==ControlAction.LowerHand){_helpAt=null;return ControlResponse.Success();}
                if(_helpSession!=caller.SessionId || _helpUser!=SessionUser(caller.SessionId))_helpAt=null;
                if(_helpAt is null)
                {
                    if(_helpCooldown.TryGetValue(caller.Sid,out var last)&&DateTimeOffset.UtcNow-last<TimeSpan.FromSeconds(20))return ControlResponse.Fail("Please wait 20 seconds before raising your hand again.");
                    _helpAt=DateTimeOffset.UtcNow;_helpCooldown[caller.Sid]=_helpAt.Value;_helpSession=caller.SessionId;_helpUser=SessionUser(caller.SessionId);
                }
                return ControlResponse.Success("Help requested.");
            }
            if(r.Action==ControlAction.ReportScreenCover)
            {
                var expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"ClassroomGuard","StudentShell","ClassroomGuard.StudentShell.exe");
                if(!string.Equals(caller.ExecutablePath,expected,StringComparison.OrdinalIgnoreCase) || (!_config.StudentUserSids.Contains(caller.Sid) && !caller.Administrator))return ControlResponse.Fail("Only installed StudentShell may report screen coverage.");
                var ok=_screenLock.Locked && _screenCover.Report(r.ScreenLockToken,r.ScreenCovered,r.MonitorCount,caller.SessionId,unchecked((int)WTSGetActiveConsoleSessionId()),DateTimeOffset.UtcNow);
                return ok?ControlResponse.Success():ControlResponse.Fail("Stale lock generation or inactive session.");
            }
            if(r.Action==ControlAction.ChangePassword)
            {
                if(!_throttles.TryGetValue("password:"+caller.Sid,out var limit))_throttles["password:"+caller.Sid]=limit=new(8,TimeSpan.FromMinutes(1));
                if(!caller.Administrator && limit.IsBlocked())return ControlResponse.Fail("Try again in one minute.");
                var ok=!string.IsNullOrEmpty(r.Username) && !string.IsNullOrEmpty(r.Password) && !string.IsNullOrEmpty(r.NewPassword) && r.Password.Length<=256 && _credentials.ChangePassword(r.Username,r.Password,r.NewPassword);
                if(ok)limit.RecordSuccess();else limit.RecordFailure();
                return ok?ControlResponse.Success("Password changed."):ControlResponse.Fail("Current password required; new password must be 12–256 characters.");
            }
            if(r.Action==ControlAction.ReportTestResult)return ControlResponse.Fail("Free-form results disabled. Submit a service-owned attempt.");
            if(r.Action is ControlAction.StartTest or ControlAction.SaveAnswers or ControlAction.FinishTest or ControlAction.ResumeTest)
            {
                if(!_config.StudentUserSids.Contains(caller.Sid) && !caller.Administrator)return ControlResponse.Fail("Windows account not enrolled as a student.");
                AttemptView? view=r.Action switch
                {
                    ControlAction.StartTest=>_attempts.Start(caller.Sid,(_lessons.Active?.Policy??_baseline).Tests.FirstOrDefault(t=>t.Id==r.TestId)??throw new ArgumentException("Test not found.")),
                    ControlAction.ResumeTest=>_attempts.Resume(caller.Sid),
                    _=>_attempts.Update(caller.Sid,r.AttemptId??"",r.Answers,r.Action==ControlAction.FinishTest)
                };
                return new(){Ok=true,Attempt=view,Message="Saved by Agent."};
            }
            if(r.Action==ControlAction.ApproveInstall)
            {
                var(ok,msg)=_installer.ApproveByCode(r.Code??"");return ok?ControlResponse.Success(msg):ControlResponse.Fail(msg);
            }
            var command=r.Action switch{ControlAction.LockScreen=>NetCommand.LockScreen,ControlAction.UnlockScreen=>NetCommand.UnlockScreen,ControlAction.GetStatus=>NetCommand.GetStatus,ControlAction.EnableStudentMode=>NetCommand.EnableStudent,ControlAction.DisableStudentMode=>NetCommand.DisableStudent,ControlAction.SetState=>NetCommand.SetState,ControlAction.Uninstall=>NetCommand.Uninstall,_=>NetCommand.Hello};
            var response=Apply(new(){Command=command,Username=r.Username,Password=r.Password,Code=r.Code??(r.Password?.Length==6?r.Password:null),State=r.State},false,caller);
            // No answer keys or other privileged fields in student IPC status.
            return new(){Ok=response.Ok,Message=response.Message,State=response.State?.StudentView(),Health=response.Health};
        }
    }
    private NetResponse HandleNet(NetRequest r)=>Apply(r,true);
}
