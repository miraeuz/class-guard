using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
namespace ClassroomGuard.Core.Enforcement;
// Intentionally unavailable: a best-effort registry SRP writer is not a safe
// production application-control provider. Never delete/overwrite existing SRP.
public sealed class SrpInstallEnforcer : IEnforcer
{
    public string Name=>"SRP capability";
    private readonly Func<bool> _armed;
    public SrpInstallEnforcer(ILogger log,Func<bool> armed){_armed=armed;}
    public void Apply(EnforcementState state){CheckLegacy();if(_armed())throw new InvalidOperationException("SRP is disabled in this release. Use a separately validated Windows application-control policy.");}
    public void Revert()=>CheckLegacy();
    private static void CheckLegacy()
    {
        using var root=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Safer\CodeIdentifiers");
        if(root?.GetValue("__ClassroomGuard") is not null)throw new InvalidOperationException("Legacy SRP detected. Restore the pre-install policy with your administrator before continuing; original values cannot be inferred safely.");
    }
}
