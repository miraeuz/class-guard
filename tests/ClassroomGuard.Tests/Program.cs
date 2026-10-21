using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ClassroomGuard.Agent;
using ClassroomGuard.Admin.Services;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Enforcement;
using ClassroomGuard.Core.Models;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Persistence;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
var root=Path.Combine(Path.GetTempPath(),"cg-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var log=NullLogger.Instance;int passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
bool Throws(Action action){try{action();return false;}catch{return true;}}
TestPackage Test()=>new(){Id="test",Title="Example",TimeLimitMinutes=5,Questions=new(){new(){Text="2+2?",Options=new(){"3","4"},CorrectIndex=1}}};
EnforcementState Policy(){var s=EnforcementState.FailClosedDefault();s.PolicyId=Guid.NewGuid().ToString("N");s.Tests.Add(Test());return s;}
try
{
    var clock=DateTimeOffset.UtcNow;
    var pausePath=Path.Combine(root,"pause-tests.json");var pauseStore=new AttemptStore(log,pausePath,()=>clock);
    var timed=pauseStore.Start("pupil",Test());clock=clock.AddSeconds(70);
    Check(pauseStore.SetPaused(true,"teacher")==1,"teacher pauses active attempt");
    var held=pauseStore.Resume("pupil")!;
    Check(held.Paused&&held.Deadline is null&&Math.Abs(held.RemainingSeconds!.Value-230)<.01,"pause stores exact remaining time");
    Check(pauseStore.SetPaused(true,"teacher")==0,"repeated pause does not grant extra time");
    Check(Throws(()=>pauseStore.Update("pupil",timed.Id,new[]{1},false)),"paused attempt rejects answer changes");
    Check(Throws(()=>pauseStore.Update("pupil",timed.Id,new[]{1},true)),"paused attempt rejects finish");
    clock=clock.AddHours(2);pauseStore=new AttemptStore(log,pausePath,()=>clock);
    Check(pauseStore.Resume("pupil")!.Paused,"pause survives restart and elapsed wall time");
    Check(pauseStore.SetPaused(false,"teacher")==1,"teacher resumes paused attempt");
    Check(Math.Abs((pauseStore.Resume("pupil")!.Deadline!.Value-clock).TotalSeconds-230)<.01,"resume restores remaining duration");
    Check(pauseStore.SetPaused(false,"teacher")==0,"repeated resume cannot extend deadline");
    var oldDisk=File.ReadAllText(pausePath);File.Delete(pausePath);Directory.CreateDirectory(pausePath);
    Check(Throws(()=>pauseStore.Update("pupil",timed.Id,new[]{1},false)),"injected disk failure rejects answer commit");
    Check(pauseStore.Resume("pupil")!.Answers[0]==-1,"failed answer commit leaves memory unchanged");
    Check(Throws(()=>pauseStore.SetPaused(true,"teacher"))&&!pauseStore.Resume("pupil")!.Paused,"failed pause write leaves timer running");
    Directory.Delete(pausePath);File.WriteAllText(pausePath,oldDisk);
    clock=clock.AddSeconds(231);
    Check(pauseStore.SetPaused(true,"teacher")==0&&pauseStore.Resume("pupil")!.Finished,"pause cannot resurrect an expired exam");
    var historyStored=JsonSerializer.Deserialize<List<StoredAttempt>>(File.ReadAllText(pausePath))!;
    Check(historyStored[0].TimingHistory.Count==2,"pause and resume timing events are durable");
    var lessonPath=Path.Combine(root,"lesson.json");var lessons=new LessonStore(lessonPath,log);
    var lesson=new LessonSession{Id=Guid.NewGuid().ToString("N"),Title="Python lesson",Group="IT-1",Policy=Policy()};
    lessons.Begin(lesson);var lessonPolicyId=lessons.Active!.Policy.PolicyId;
    lesson.Policy.Sites.Add("external.example");
    Check(lessons.Active.Policy.Sites.Count==0,"lesson stores an isolated policy snapshot");
    Check(new LessonStore(lessonPath,log).Active?.Id==lesson.Id,"lesson survives service restart");
    lessons.Begin(lesson);Check(lessons.Active.Policy.PolicyId==lessonPolicyId,"retry of same lesson is idempotent");
    Check(Throws(()=>lessons.End("other-session"))&&lessons.Active is not null,"stale lesson end cannot stop another lesson");
    Check(Throws(()=>lessons.Begin(new(){Id=Guid.NewGuid().ToString("N"),Title="Other",Policy=Policy()})),"active lesson cannot be silently replaced");
    var savedLesson=File.ReadAllText(lessonPath);File.Delete(lessonPath);Directory.CreateDirectory(lessonPath);
    Check(Throws(()=>lessons.End(lesson.Id))&&lessons.Active is not null,"failed lesson end write retains active lesson");
    Directory.Delete(lessonPath);File.WriteAllText(lessonPath,savedLesson);lessons.End(lesson.Id);
    Check(new LessonStore(lessonPath,log).Active is null,"lesson end persists");
    var auditPath=Path.Combine(root,"activity.json");var journal=new ActivityJournal(auditPath,log);var auditFake=new FakeClient();var audited=new AuditedAdminClient(auditFake,()=>journal);
    await audited.SendAsync(new(){MachineId="logged-device"},new(){Command=NetCommand.LockScreen});
    Check(journal.Read().Count==2&&journal.Read()[0].Result=="Requested"&&journal.Read()[1].Result=="Succeeded","activity records request and outcome");
    await audited.SendAsync(new(){MachineId="logged-device"},new(){Command=NetCommand.GetStatus});
    Check(journal.Read().Count==2,"routine status polling does not flood activity history");
    auditFake.Reject=true;await audited.SendAsync(new(){MachineId="logged-device"},new(){Command=NetCommand.UnlockScreen});
    Check(journal.Read().Last().Result=="Failed","failed command remains in activity history");
    var commandsBefore=auditFake.Commands;File.Delete(auditPath);Directory.CreateDirectory(auditPath);
    Check(!(await audited.SendAsync(new(){MachineId="logged-device"},new(){Command=NetCommand.LockScreen})).Ok&&auditFake.Commands==commandsBefore,"journal write failure prevents unrecorded command dispatch");
    Directory.Delete(auditPath);
    var targetTime=DateTimeOffset.UtcNow;
    var targetPc=new MachineInfo { MachineId="pc-a",Ip="192.168.1.2",Port=8787,Verified=true,LastSeen=targetTime,LastVerified=targetTime };
    Check(DashboardTargeting.CanTarget(targetPc,targetTime),"fresh verified endpoint can receive selected action");
    targetPc.IdentityConflict=true;
    Check(!DashboardTargeting.CanTarget(targetPc,targetTime),"identity conflict excluded even with cached verification");
    targetPc.IdentityConflict=false;
    Check(!DashboardTargeting.CanTarget(targetPc,targetTime.AddSeconds(46)),"stale verification excludes selected target");
    var oldEndpoint=DashboardTargeting.EndpointKey(targetPc);targetPc.Ip="192.168.1.3";
    Check(oldEndpoint!=DashboardTargeting.EndpointKey(targetPc),"selection does not silently follow changed IP");
    var cover=new ScreenCoverTracker();var coverNow=DateTimeOffset.UtcNow;
    Check(!cover.IsFresh(1,coverNow),"screen confirmation absent before shell report");
    Check(!cover.Report("old",true,1,1,1,coverNow),"old screen token rejected");
    Check(!cover.Report(cover.Token,true,1,2,1,coverNow),"background session cannot confirm console screen");
    Check(!cover.Report(cover.Token,true,0,1,1,coverNow),"zero monitor confirmation rejected");
    Check(cover.Report(cover.Token,true,2,1,1,coverNow)&&cover.IsFresh(1,coverNow),"two-monitor current-generation report accepted");
    Check(!cover.IsFresh(1,coverNow.AddSeconds(9)),"screen report expires after lost heartbeat");
    Check(!cover.IsFresh(2,coverNow),"console switch invalidates screen confirmation");
    Check(!cover.IsFresh(1,coverNow.AddSeconds(-1)),"future screen report is not trusted");
    cover.Report(cover.Token,false,2,1,1,coverNow);
    Check(!cover.IsFresh(1,coverNow),"uncovered report removes confirmation");
    var previousToken=cover.Token;cover.Reset();
    Check(cover.Token!=previousToken&&!cover.Report(previousToken,true,1,1,1,coverNow),"new lock generation rejects replayed heartbeat");
    var screenPath=Path.Combine(root,"screen-lock.json");
    var screenStore=new ScreenLockStore(screenPath,log);
    Check(!screenStore.Locked,"first installation starts screen unlocked");
    screenStore.Set(true);
    Check(new ScreenLockStore(screenPath,log).Locked,"screen lock survives service reconstruction / reboot");
    screenStore.Set(true);
    Check(new ScreenLockStore(screenPath,log).Locked,"repeat lock is idempotent");
    screenStore.Set(false);
    Check(!new ScreenLockStore(screenPath,log).Locked,"explicit release persists");
    File.WriteAllText(screenPath,"invalid json");
    Check(new ScreenLockStore(screenPath,log).Locked,"corrupted lock state fails closed");
    File.WriteAllText(screenPath,"{}");
    Check(new ScreenLockStore(screenPath,log).Locked,"missing lock property fails closed");
    screenStore.Set(true);
    File.Delete(screenPath);Directory.CreateDirectory(screenPath);
    Check(Throws(()=>screenStore.Set(false)) && screenStore.Locked,"failed durable unlock does not change in-memory lock");
    Directory.Delete(screenPath);
    Check((int)NetCommand.Ping==7 && (int)NetCommand.LockScreen==8,"existing network command IDs preserved");
    Check((int)ClassroomGuard.Core.Ipc.ControlAction.ReportTestResult==11 && (int)ClassroomGuard.Core.Ipc.ControlAction.UnlockScreen==13,"existing IPC command IDs preserved");
    using(var discovery=new Discovery(0))
    {
        discovery.Start();
        for(var i=1;i<=10;i++)
        {
            using var sender=new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0."+i),0));
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new AnnouncePacket{MachineId="udp-clone",Name="UDP-PC-"+i,Port=43210});
            await sender.SendAsync(bytes,bytes.Length,new IPEndPoint(IPAddress.Loopback,discovery.BoundPort));
        }
        for(var i=0;i<100 && discovery.Machines.Count<10;i++)await Task.Delay(20);
        Check(discovery.Machines.Count==10,"UDP listener retains all ten simultaneous cloned-ID endpoints");
    }
    var catalog=new DiscoveryCatalog();var now=DateTimeOffset.UtcNow;
    for(var i=1;i<=10;i++)Check(catalog.Observe(new(){MachineId="cloned-windows-id",Name="PC-"+i,Port=43210},"192.168.1."+i,now),"discovery accepts endpoint "+i);
    var found=catalog.Snapshot(now);Check(found.Count==10,"ten cloned IDs remain ten visible endpoints");
    Check(found.All(m=>m.IdentityConflict),"duplicate live identities flagged before commands");
    catalog.Observe(new(){MachineId="cloned-windows-id",Name="PC-1",Port=43210},"192.168.1.1",now.AddSeconds(50));
    Check(catalog.Snapshot(now.AddSeconds(50)).Count==10,"offline endpoints stay visible");
    Check(catalog.Snapshot(now.AddSeconds(50)).All(m=>!m.IdentityConflict),"expired endpoints do not permanently block a moved IP");
    Check(!catalog.Observe(new(){MachineId="bad",Name=null!,Port=43210},"192.168.1.99",now),"malformed null announcement rejected");
    Check(!catalog.Observe(new(){MachineId="bad",Name="PC",Port=1},"192.168.1.99",now),"invalid announcement port rejected");
    catalog.Observe(new(){MachineId="repaired-id",Name="PC-1",Port=43210},"192.168.1.1",now.AddSeconds(51));
    Check(catalog.Snapshot(now.AddSeconds(51)).Count==10 && catalog.Snapshot(now.AddSeconds(51)).Any(m=>m.MachineId=="repaired-id"),"re-enrollment replaces the same endpoint without a ghost row");
    var independent=new DiscoveryCatalog();for(var i=1;i<=10;i++)independent.Observe(new(){MachineId="id"+i,Name="SAME-NAME",Port=43210},"192.168.2."+i,now);
    Check(independent.Snapshot(now).Count==10 && independent.Snapshot(now).All(m=>!m.IdentityConflict),"same computer names do not merge distinct identities");
    var rich=Policy();rich.Tests[0].ShowReviewAfterFinish=true;
    var richQ=rich.Tests[0].Questions[0];richQ.TextHtml="<p><b>2+2?</b></p>";richQ.Code="print(2+2)";richQ.Latex="2+2";richQ.Explanation="Four";
    PolicyValidator.Validate(rich);Check(rich.Clone().Tests[0].Questions[0].Code==richQ.Code,"rich fields survive policy cloning");
    Check(rich.StudentView().Tests[0].Questions[0].Explanation=="","student policy hides explanations");
    var richAttempt=new StoredAttempt{Test=rich.Tests[0],Answers=new[]{1}};
    Check(richAttempt.View().Test.Questions[0].CorrectIndex==-1 && richAttempt.View().Test.Questions[0].Explanation=="","active rich attempts hide answers and explanations");
    richAttempt.Finished=true;Check(richAttempt.View().Test.Questions[0].CorrectIndex==1 && richAttempt.View().Test.Questions[0].Explanation=="Four","opt-in finished review includes explanation");
    richAttempt.Test.ShowReviewAfterFinish=false;Check(richAttempt.View().Test.Questions[0].CorrectIndex==-1,"review remains disabled by default");
    richQ.TextHtml=new string('a',12001);Check(Throws(()=>PolicyValidator.Validate(rich)),"oversized rich content rejected");
    var policy=Policy();PolicyValidator.Validate(policy);Check(true,"valid policy accepted");
    var invalid=policy.Clone();invalid.Tests[0].Questions[0].CorrectIndex=-1;Check(Throws(()=>PolicyValidator.Validate(invalid)),"negative correct index rejected");
    invalid=policy.Clone();invalid.Tests[0].Questions[0].Options=new(){"A","a"};Check(Throws(()=>PolicyValidator.Validate(invalid)),"duplicate options rejected");
    invalid=policy.Clone();invalid.Tests=null!;Check(Throws(()=>PolicyValidator.Validate(invalid)),"null tests rejected");
    invalid=policy.Clone();invalid.SiteMode=SiteBlockingMode.Whitelist;Check(Throws(()=>PolicyValidator.Validate(invalid)),"unsupported whitelist rejected");
    invalid=policy.Clone();invalid.Sites.Add("example.com\n127.0.0.1 other.example");Check(Throws(()=>PolicyValidator.Validate(invalid)),"hosts newline injection rejected");
    invalid=policy.Clone();invalid.StudentModeActive=false;Check(Throws(()=>PolicyValidator.Validate(invalid)),"unrestricted baseline rejected");
    Check(policy.StudentView().Tests[0].Questions[0].CorrectIndex==-1 && policy.Tests[0].Questions[0].CorrectIndex==1,"student view hides key without mutating baseline");
    Check(!PolicyValidator.LibraryTargetAllowed("online","file:///C:/evil.exe"),"library file URI rejected for online item");
    Check(!PolicyValidator.LibraryTargetAllowed("online","https://user:password@example.com"),"credential-bearing URL rejected");
    Check(Throws(()=>ClassroomKey.FromBase64(Convert.ToBase64String(new byte[2]))),"weak classroom key rejected");
    Check(Totp.Generate(Encoding.ASCII.GetBytes("12345678901234567890"),DateTimeOffset.FromUnixTimeSeconds(59))=="287082","RFC TOTP vector (six digits)");
    var credentials=new CredentialStore(Path.Combine(root,"users.json"),Path.Combine(root,"bootstrap.txt"),log);credentials.EnsureBootstrapUser();
    var text=File.ReadAllText(Path.Combine(root,"bootstrap.txt"));var old=text.Split('\n').Single(x=>x.StartsWith("password: "))[10..].Trim();
    Check(!credentials.ChangePassword("teacher","wrong","NewPassword!123"),"password change requires current password");
    Check(credentials.ChangePassword("teacher",old,"NewPassword!123"),"bootstrap enrollment authenticated");
    Check(!File.Exists(Path.Combine(root,"bootstrap.txt")),"bootstrap secret deleted");
    Check(credentials.Verify("teacher","NewPassword!123",out var mustChange)&&!mustChange,"new credential usable");
    var path=Path.Combine(root,"policy.json");var store=new PolicyStore(path,log);store.Save(policy);
    File.WriteAllText(path,"{}");Check(store.Load().PolicyId==policy.PolicyId,"incomplete primary policy recovers known-good baseline");
    File.WriteAllText(path,JsonSerializer.Serialize(new EnforcementState{StudentModeActive=false}));Check(store.Load().StudentModeActive,"persisted teacher mode never resumes");
    var attemptsPath=Path.Combine(root,"attempts.json");var attempts=new AttemptStore(log,attemptsPath);var view=attempts.Start("student-a",Test());
    Check(view.Test.Questions[0].CorrectIndex==-1,"attempt response excludes keys");
    Check(Throws(()=>attempts.Update("student-b",view.Id,new[]{1},true)),"attempt bound to account SID");
    Check(Throws(()=>attempts.Update("student-a",view.Id,new[]{8},true)),"out-of-range submitted answer rejected");
    attempts.Update("student-a",view.Id,new[]{1},false);
    var resumed=new AttemptStore(log,attemptsPath).Resume("student-a");Check(resumed?.Answers[0]==1,"answers durable across service object restart");
    var done=attempts.Update("student-a",view.Id,new[]{1},true);Check(done.Finished&&done.Correct==1,"service computes score");
    Check(attempts.Update("student-a",view.Id,new[]{0},true).Correct==1,"finished attempt immutable and submit idempotent");
    var expiredPath=Path.Combine(root,"expired.json");File.WriteAllText(expiredPath,JsonSerializer.Serialize(new[]{new StoredAttempt{Id="expired",OwnerSid="student-a",Test=Test(),Answers=new[]{-1},Deadline=DateTimeOffset.UtcNow.AddSeconds(-1)}}));
    var expired=new AttemptStore(log,expiredPath).Update("student-a","expired",new[]{1},true);Check(expired.Finished && expired.Correct==0,"answers after deadline cannot change score");
    var codes=new InstallCodeStore();var code=codes.Issue("test.exe","hash").Code;Check(codes.Redeem(code) is not null && codes.Redeem(code) is null,"install approval code consumed once");
    var coordinator=new EnforcementCoordinator(log,()=>false,new IEnforcer[]{new FailingEnforcer()});Check(!coordinator.ApplyStudentModeOrRelease(policy).Healthy,"enforcer failure is surfaced");
    var fake=new FakeClient();var syncPath=Path.Combine(root,"intent.json");var sync=new SyncEngine(fake,syncPath);var machine=new MachineInfo{MachineId="device"};
    await sync.ReconcileAsync(machine);Check(fake.Pushes==0,"no reconciliation before baseline initialized");
    sync.SetIntended(Policy());var a=sync.Intended!;fake.State=a.Clone();fake.State.Version=1000;
    sync.SetIntended(Policy());await sync.ReconcileAsync(machine);Check(fake.Pushes==1 && fake.State.PolicyId==sync.Intended!.PolicyId,"different policy ID reconciles despite higher agent version");
    await sync.ReconcileAsync(machine);Check(fake.Pushes==1,"identical policy does not re-lock or repush");
    Check(new SyncEngine(fake,syncPath).Intended?.PolicyId==sync.Intended!.PolicyId,"Admin baseline survives recreation");
    var targeted=Policy();targeted.Tests[0].Title="Selected test";
    sync.SetDeviceIntended(new[]{"device"},targeted);
    await sync.ReconcileAsync(machine);
    Check(fake.State.Tests[0].Title=="Selected test","targeted test policy reaches only intended device");
    Check(sync.ForDevice("another-device")!.PolicyId==sync.Intended!.PolicyId,"unselected device retains baseline policy");
    Check(new SyncEngine(fake,syncPath).ForDevice("device")!.Tests[0].Title=="Selected test","targeted policy survives teacher restart");
    fake.LessonId="active-lesson";fake.State=Policy();var beforeLessonPoll=fake.Pushes;
    await sync.ReconcileAsync(machine);Check(fake.Pushes==beforeLessonPoll,"automatic synchronization does not overwrite active lesson");
    fake.LessonId=null;await sync.ReconcileAsync(machine);Check(fake.State.Tests[0].Title=="Selected test","targeted policy returns after lesson ends");
    sync.SetIntended(Policy());Check(sync.ForDevice("device")!.PolicyId==sync.Intended!.PolicyId,"explicit global baseline supersedes older targeted assignments");
    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("one\ntwo\n")))using(var reader=new StreamReader(stream))
    {Check(await Wire.ReadAsync(reader,CancellationToken.None)=="one" && await Wire.ReadAsync(reader,CancellationToken.None)=="two","bounded framing preserves consecutive messages");}
    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(new string('x',Wire.MaxChars+1)+"\n")))using(var reader=new StreamReader(stream))
    {var rejected=false;try{await Wire.ReadAsync(reader,CancellationToken.None);}catch(IOException){rejected=true;}Check(rejected,"oversized frame rejected");}
    var key=ClassroomKey.Generate();
    // Same certificate code path the Agent service uses (create, then reload from disk).
    DeviceIdentity.LoadOrCreate(root).Dispose();using var cert=DeviceIdentity.LoadOrCreate(root);
    // Fake agent: real TLS, then a hello with the supplied certificate proof. Records whether the
    // console ever sent its authentication (it must not, unless the proof is valid).
    async Task<(NetResponse Result,bool SentAuth)> Agent(string? proof)
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var target=new MachineInfo{MachineId="device",Ip="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port};
        using var timeout=new CancellationTokenSource(5000);var sentAuth=false;
        var server=Task.Run(async()=>
        {
            try
            {
                using var tcp=await listener.AcceptTcpClientAsync(timeout.Token);using var tls=new SslStream(tcp.GetStream());
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=cert},timeout.Token);
                using var r=new StreamReader(tls,leaveOpen:true);using var w=new StreamWriter(tls,leaveOpen:true){AutoFlush=true};
                await Wire.WriteAsync(w,JsonSerializer.Serialize(new NetResponse{Ok=true,Nonce="abc",CertProof=proof}),timeout.Token);
                var auth=JsonSerializer.Deserialize<NetRequest>(await Wire.ReadAsync(r,timeout.Token)??"");
                sentAuth=auth?.Command==NetCommand.Auth;
                if(sentAuth && key.VerifyHmac("abc",auth!.AuthHmac??""))
                {
                    await Wire.WriteAsync(w,JsonSerializer.Serialize(NetResponse.Success()),timeout.Token);
                    await Wire.ReadAsync(r,timeout.Token);
                    await Wire.WriteAsync(w,JsonSerializer.Serialize(NetResponse.Success("pong")),timeout.Token);
                }
            }
            catch{}
        });
        var result=await new AdminClient(key).SendAsync(target,new(){Command=NetCommand.Ping},3000);
        await server;listener.Stop();return (result,sentAuth);
    }
    var fp=DeviceIdentity.Fingerprint(cert);
    var noProof=await Agent(null);Check(!noProof.Result.Ok && !noProof.SentAuth,"agent without certificate proof rejected before console authenticates");
    var foreign=await Agent(ClassroomKey.Generate().Hmac(DeviceIdentity.ProofInput(fp)));Check(!foreign.Result.Ok && !foreign.SentAuth,"agent from another classroom rejected before console authenticates");
    var stolen=await Agent(key.Hmac(DeviceIdentity.ProofInput(new string('0',64))));Check(!stolen.Result.Ok && !stolen.SentAuth,"proof for a different certificate rejected");
    var good=await Agent(key.Hmac(DeviceIdentity.ProofInput(fp)));Check(good.Result.Ok && good.Result.Message=="pong","agent with valid classroom proof trusted automatically (no enrollment)");
    Console.WriteLine($"ALL {passed} REGRESSION CHECKS PASSED");return 0;
}
finally{Directory.Delete(root,true);}
sealed class FailingEnforcer:IEnforcer{public string Name=>"test";public void Apply(EnforcementState s)=>throw new IOException("injected failure");public void Revert()=>throw new IOException("injected failure");}
sealed class FakeClient:IAdminClient
{
    public int Pushes;public int Commands;public bool Reject;public string? LessonId;public EnforcementState State=EnforcementState.FailClosedDefault();
    public Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000)
    {
        if(request.Command!=NetCommand.GetStatus)Commands++;
        if(request.Command==NetCommand.SetState&&!Reject){State=request.State!.Clone();Pushes++;}
        return Task.FromResult(new NetResponse{Ok=!Reject,State=State.Clone(),Machine=new(){MachineId=machine.MachineId},Health=new(){StudentMode=true,LessonId=LessonId}});
    }
}
