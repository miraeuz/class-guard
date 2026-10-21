using ClassroomGuard.Core.Net;
namespace ClassroomGuard.Admin.Services;
public static class DashboardTargeting
{
    public static string EndpointKey(MachineInfo m)=>$"{m.MachineId}|{m.Ip}:{m.Port}";
    public static bool CanTarget(MachineInfo m,DateTimeOffset now)=>!m.IdentityConflict && m.Verified && now>=m.LastVerified && now-m.LastVerified<TimeSpan.FromSeconds(45) && now-m.LastSeen<TimeSpan.FromSeconds(45);
}
