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
    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("one\ntwo\n")))using(var reader=new StreamReader(stream))
    {Check(await Wire.ReadAsync(reader,CancellationToken.None)=="one" && await Wire.ReadAsync(reader,CancellationToken.None)=="two","bounded framing preserves consecutive messages");}
    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(new string('x',Wire.MaxChars+1)+"\n")))using(var reader=new StreamReader(stream))
    {var rejected=false;try{await Wire.ReadAsync(reader,CancellationToken.None);}catch(IOException){rejected=true;}Check(rejected,"oversized frame rejected");}
    var key=ClassroomKey.Generate();var missing=new AdminClient(key,new Dictionary<string,string>());
    Check(!(await missing.SendAsync(machine,new(){Command=NetCommand.GetStatus})).Ok,"unenrolled device rejected before connecting");
    // Real TLS test: valid-looking server with the wrong pinned certificate.
    using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=ClassroomGuardAgent",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
    using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();machine.Ip="127.0.0.1";machine.Port=((IPEndPoint)listener.LocalEndpoint).Port;
    using var timeout=new CancellationTokenSource(5000);
    var server=Task.Run(async()=>{using var tcp=await listener.AcceptTcpClientAsync(timeout.Token);using var tls=new SslStream(tcp.GetStream());try{await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=cert},timeout.Token);}catch{} });
    var pinned=new AdminClient(key,new Dictionary<string,string>{{"device",new string('0',64)}});
    var rejectedCert=await pinned.SendAsync(machine,new(){Command=NetCommand.GetStatus},3000);await server;listener.Stop();Check(!rejectedCert.Ok,"real TLS connection rejects mismatched certificate");
    Console.WriteLine($"ALL {passed} REGRESSION CHECKS PASSED");return 0;
}
finally{Directory.Delete(root,true);}
sealed class FailingEnforcer:IEnforcer{public string Name=>"test";public void Apply(EnforcementState s)=>throw new IOException("injected failure");public void Revert()=>throw new IOException("injected failure");}
sealed class FakeClient:IAdminClient
{
    public int Pushes;public EnforcementState State=EnforcementState.FailClosedDefault();
    public Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000)
    {
        if(request.Command==NetCommand.SetState){State=request.State!.Clone();Pushes++;}
        return Task.FromResult(new NetResponse{Ok=true,State=State.Clone(),Machine=new(){MachineId=machine.MachineId},Health=new(){StudentMode=true}});
    }
}
