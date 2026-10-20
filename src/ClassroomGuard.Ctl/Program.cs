using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Enforcement;
using ClassroomGuard.Core.Ipc;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
if(args.Length==0){Console.WriteLine("cgctl screen-lock | screen-unlock [teacher] | status | enable | disable [teacher] | passwd [teacher] | uninstall [teacher] | code | approveinstall CODE | emergency-restore");return 1;}
try
{
    var action=args[0].ToLowerInvariant();
    if(action=="emergency-screen-unlock")
    {
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException("Run elevated.");
        if(System.Diagnostics.Process.GetProcessesByName("ClassroomGuard.Agent").Length>0)throw new InvalidOperationException("Stop ClassroomGuardAgent first, then run emergency-screen-unlock and start it again.");
        new ClassroomGuard.Core.Persistence.ScreenLockStore(Path.Combine(AppPaths.DataDir,"screen-lock.json"),NullLogger.Instance).Set(false);
        Console.WriteLine("Durable screen lock cleared. Start ClassroomGuardAgent to release the student screen.");return 0;
    }
    if(action=="emergency-restore")
    {
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException("Run elevated.");
        var log=NullLogger.Instance;
        // Use only durable owned backups. Never blindly delete school policies.
        new SrpInstallEnforcer(log,()=>false).Revert();
        new DownloadEnforcer(log).Revert();
        new HostsEnforcer(log).Revert();
        Console.WriteLine("Owned policies restored. Keep the service stopped during removal.");return 0;
    }
    if(action=="code")
    {
        var key=ClassroomKey.FromBase64(File.ReadAllText(AppPaths.ClassroomKeyFile).Trim());
        Console.WriteLine(Totp.Now(key.TotpSeed(MachineIdentity.MachineId)));return 0;
    }
    var req=new ControlRequest{Username=args.Length>1?args[1]:"teacher"};
    switch(action)
    {
        case "screen-lock":req.Action=ControlAction.LockScreen;req.Password=Secret("Teacher password (blank when elevated): ");break;
        case "screen-unlock":req.Action=ControlAction.UnlockScreen;req.Password=Secret("Teacher password / recovery code (blank when elevated): ");break;
        case "status":req.Action=ControlAction.GetStatus;break;
        case "enable":req.Action=ControlAction.EnableStudentMode;break;
        case "approveinstall":req.Action=ControlAction.ApproveInstall;req.Code=args.Length>1?args[1]:"";break;
        case "disable":req.Action=ControlAction.DisableStudentMode;req.Password=Secret("Teacher password / recovery code: ");break;
        case "uninstall":req.Action=ControlAction.Uninstall;req.Password=Secret("Teacher password / code (blank for elevated recovery): ");break;
        case "passwd":
            req.Action=ControlAction.ChangePassword;req.Password=Secret("Current / bootstrap password: ");req.NewPassword=Secret("New password (12+ characters): ");
            if(req.NewPassword!=Secret("Repeat new password: "))throw new ArgumentException("Passwords differ.");break;
        default:throw new ArgumentException("Unknown command. Raw block/unblock commands were removed; use validated Admin policy editing.");
    }
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var pipe=await LocalPipeClient.ConnectAsync(timeout.Token);
    using var reader=new StreamReader(pipe,leaveOpen:true);using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};
    await Wire.WriteAsync(writer,JsonSerializer.Serialize(req),timeout.Token);
    var response=JsonSerializer.Deserialize<ControlResponse>(await Wire.ReadAsync(reader,timeout.Token)??"")??throw new IOException("No response.");
    Console.WriteLine(JsonSerializer.Serialize(response,new JsonSerializerOptions{WriteIndented=true}));return response.Ok?0:2;
}
catch(Exception ex){Console.Error.WriteLine(ex.Message);return 3;}
static string Secret(string prompt)
{
    Console.Write(prompt);var text=new System.Text.StringBuilder();
    while(true){var k=Console.ReadKey(true);if(k.Key==ConsoleKey.Enter){Console.WriteLine();return text.ToString();}if(k.Key==ConsoleKey.Backspace){if(text.Length>0)text.Length--;continue;}if(!char.IsControl(k.KeyChar)&&text.Length<256)text.Append(k.KeyChar);}
}
