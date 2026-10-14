using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClassroomGuard.Core.Net;

namespace ClassroomGuard.Admin.Services;

/// <summary>
/// Listens for agent UDP announces and maintains a live machine list. IP changes
/// are self-healing because machines are keyed by MachineId, not IP (spec §2).
/// </summary>
public sealed class Discovery : IDisposable
{
    private readonly int _port;
    private readonly ConcurrentDictionary<string, MachineInfo> _machines = new();
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;

    public Discovery(int announcePort) => _port = announcePort;

    public IReadOnlyCollection<MachineInfo> Machines => _machines.Values.ToList();

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port)) { EnableBroadcast = true };
        _ = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udp is not null)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var pkt = JsonSerializer.Deserialize<AnnouncePacket>(json);
                if (pkt is null || string.IsNullOrEmpty(pkt.MachineId) || pkt.MachineId.Length>100 || pkt.Name.Length>100 || pkt.Port is <1024 or >65535) continue;
                if (_machines.Count>=256 && !_machines.ContainsKey(pkt.MachineId))continue;
                // Announcements are untrusted hints. Never follow an arbitrary payload IP.
                pkt.Ip=result.RemoteEndPoint.Address.ToString();

                _machines.AddOrUpdate(pkt.MachineId,
                    _ => FromPacket(pkt),
                    (_, existing) =>
                    {
                        existing.Name = pkt.Name;
                        existing.Ip = pkt.Ip;
                        existing.Port = pkt.Port;
                        existing.Version = pkt.Version;
                        existing.StudentMode = pkt.StudentMode;
                        existing.LastSeen = DateTimeOffset.UtcNow;
                        return existing;
                    });
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore malformed packets */ }
        }
    }

    private static MachineInfo FromPacket(AnnouncePacket p) => new()
    {
        MachineId = p.MachineId,
        Name = p.Name,
        Ip = p.Ip,
        Port = p.Port,
        Version = p.Version,
        StudentMode = p.StudentMode,
        LastSeen = DateTimeOffset.UtcNow,
    };

    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Dispose();
    }
}
