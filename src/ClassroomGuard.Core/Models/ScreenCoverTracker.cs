namespace ClassroomGuard.Core.Models;

/// <summary>Fresh display reports, scoped to one lock generation and console session. Not OS attestation.</summary>
public sealed class ScreenCoverTracker
{
    public string Token { get; private set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? ReportedAt { get; private set; }
    public int Session { get; private set; } = -1;
    public int MonitorCount { get; private set; }
    public void Reset() { Token=Guid.NewGuid().ToString("N");ReportedAt=null;Session=-1;MonitorCount=0; }
    public bool Report(string? token, bool covered, int monitors, int session, int activeSession, DateTimeOffset now)
    {
        if(token!=Token || session<0 || session!=activeSession || monitors<0 || monitors>32 || (covered && monitors==0))return false;
        ReportedAt=covered?now:null;Session=session;MonitorCount=covered?monitors:0;return true;
    }
    public bool IsFresh(int activeSession,DateTimeOffset now)=>Session==activeSession && ReportedAt is {} at && now>=at && now-at<=TimeSpan.FromSeconds(8);
}
