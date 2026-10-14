using ClassroomGuard.Core.Models;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Core.Enforcement;
public sealed class EnforcementCoordinator
{
    private readonly IReadOnlyList<IEnforcer> _enforcers;
    private readonly ILogger _log;
    public EnforcementCoordinator(ILogger log,Func<bool> gate,IReadOnlyList<IEnforcer> enforcers){_log=log;_enforcers=enforcers;}
    public AgentHealth ApplyStudentModeOrRelease(EnforcementState state)
    {
        var h=new AgentHealth{StudentMode=state.StudentModeActive};
        foreach(var e in _enforcers)
        {
            try { if(state.StudentModeActive)e.Apply(state);else e.Revert(); h.Controls.Add(new(e.Name,true,"Applied / verified")); }
            catch(Exception ex){_log.LogError(ex,"Enforcer {Name} failed",e.Name);h.Controls.Add(new(e.Name,false,ex.Message));}
        }
        return h;
    }
    public AgentHealth RevertAll()=>ApplyStudentModeOrRelease(new EnforcementState{StudentModeActive=false});
}
