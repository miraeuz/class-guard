namespace ClassroomGuard.Core.Models;
public sealed record EnforcerHealth(string Name, bool Ok, string Detail);
public sealed class AgentHealth
{
    public bool StudentMode { get; set; }
    public DateTimeOffset? UnlockUntil { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<EnforcerHealth> Controls { get; set; } = new();
    public bool Healthy => Controls.All(x=>x.Ok);
    public string Summary => !Healthy ? "Himoya qisman ishlayapti — o‘qituvchiga ayting" : StudentMode ? "O‘quv rejimi faol (cheklangan himoya)" : "Vaqtinchalik o‘qituvchi rejimi";
}
