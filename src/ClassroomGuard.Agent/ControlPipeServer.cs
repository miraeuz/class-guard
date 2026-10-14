using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Ipc;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Agent;
public sealed record LocalCaller(string Sid,bool Administrator,int SessionId);
public sealed class ControlPipeServer
{
    private readonly ILogger _log;
    private readonly Func<ControlRequest,LocalCaller,ControlResponse> _handler;
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle h,out uint pid);
    public ControlPipeServer(ILogger log,Func<ControlRequest,LocalCaller,ControlResponse> handler){_log=log;_handler=handler;}
    public async Task RunAsync(CancellationToken ct)
    {
        // Fixed listener count bounds clients and keeps recovery capacity available.
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>ListenAsync(ct)));
    }
    private async Task ListenAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                var security=new PipeSecurity();
                security.SetOwner(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null));
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid,null),PipeAccessRights.ReadData|PipeAccessRights.WriteData|PipeAccessRights.ReadAttributes|PipeAccessRights.ReadExtendedAttributes|PipeAccessRights.ReadPermissions|PipeAccessRights.Synchronize,AccessControlType.Allow));
                using var pipe=NamedPipeServerStreamAcl.Create(AppPaths.PipeName,PipeDirection.InOut,8,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
                await pipe.WaitForConnectionAsync(ct);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(8));
                using var reader=new StreamReader(pipe,leaveOpen:true);
                using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};
                var line=await Wire.ReadAsync(reader,deadline.Token);
                LocalCaller? caller=null;
                var session=-1;
                if(GetNamedPipeClientProcessId(pipe.SafePipeHandle,out var pid))
                    using(var p=System.Diagnostics.Process.GetProcessById((int)pid))session=p.SessionId;
                pipe.RunAsClient(()=>
                {
                    using var identity=WindowsIdentity.GetCurrent();
                    caller=new(identity.User?.Value??"",new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),session);
                });
                var request=JsonSerializer.Deserialize<ControlRequest>(line??"")??throw new InvalidDataException();
                ControlResponse response;
                try{response=_handler(request,caller??throw new UnauthorizedAccessException());}
                catch(Exception ex){_log.LogWarning(ex,"Local request failed");response=ControlResponse.Fail(ex.Message);}
                await Wire.WriteAsync(writer,JsonSerializer.Serialize(response),deadline.Token);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){_log.LogDebug(ex,"Pipe session ended");await Task.Delay(100,ct);}
        }
    }
}
