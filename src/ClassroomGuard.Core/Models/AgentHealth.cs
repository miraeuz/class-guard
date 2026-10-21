namespace ClassroomGuard.Core.Models;
public sealed record EnforcerHealth(string Name, bool Ok, string Detail);
public sealed class AgentHealth
{
    public string? LessonId { get; set; }
    public string? LessonTitle { get; set; }
    public string? LessonGroup { get; set; }
    public bool HelpRequested { get; set; }
    public DateTimeOffset? HelpRequestedAt { get; set; }
    public int PausedTests { get; set; }
    public string ScreenLockToken { get; set; } = "";
    public bool ScreenCoverConfirmed { get; set; }
    public DateTimeOffset? ScreenCoverReportedAt { get; set; }
    public double ScreenCoverAgeSeconds { get; set; } = 8;
    public int ScreenMonitorCount { get; set; }
    public bool ScreenLocked { get; set; }
    public bool StudentMode { get; set; }
    public DateTimeOffset? UnlockUntil { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<EnforcerHealth> Controls { get; set; } = new();
    public bool Healthy => Controls.All(x=>x.Ok);
    public string Summary => !Healthy ? "Himoya qisman ishlayapti — o‘qituvchiga ayting" : StudentMode ? "O‘quv rejimi faol (cheklangan himoya)" : "Vaqtinchalik o‘qituvchi rejimi";
}
