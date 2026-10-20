using System.Net;
using ClassroomGuard.Core.Net;
namespace ClassroomGuard.Admin.Services;

// UDP is an untrusted location hint, not a unique identity or authorization source.
public sealed class DiscoveryCatalog
{
    private readonly object _gate=new();
    private readonly Dictionary<string,MachineInfo> _machines=new(StringComparer.Ordinal);
    public bool Observe(AnnouncePacket? packet,string sourceIp,DateTimeOffset now)
    {
        if(packet is null || string.IsNullOrWhiteSpace(packet.MachineId) || packet.MachineId.Length>100 || string.IsNullOrWhiteSpace(packet.Name) || packet.Name.Length>100 || packet.Port is <1024 or >65535 || !IPAddress.TryParse(sourceIp,out var address))return false;
        var key=$"{address}|{packet.Port}";
        lock(_gate)
        {
            foreach(var stale in _machines.Where(x=>now-x.Value.LastSeen>TimeSpan.FromHours(24)).Select(x=>x.Key).ToArray())_machines.Remove(stale);
            if(!_machines.TryGetValue(key,out var m) || m.MachineId!=packet.MachineId)
            {
                if(_machines.Count>=256 && !_machines.ContainsKey(key))return false;
                _machines[key]=m=new(){MachineId=packet.MachineId,Ip=address.ToString(),Port=packet.Port};
            }
            m.Name=packet.Name;m.Version=packet.Version??"";m.StudentMode=packet.StudentMode;m.LastSeen=now;
            return true;
        }
    }
    public IReadOnlyCollection<MachineInfo> Snapshot(DateTimeOffset now)
    {
        lock(_gate)
        {
            var active=_machines.Values.Where(m=>now-m.LastSeen<TimeSpan.FromSeconds(45)).GroupBy(m=>m.MachineId).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet();
            foreach(var m in _machines.Values)m.IdentityConflict=active.Contains(m.MachineId);
            return _machines.Values.ToArray();
        }
    }
}
