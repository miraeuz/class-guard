using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Net;
using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Agent.Net;

/// <summary>
/// Broadcasts an <see cref="AnnouncePacket"/> to the subnet every 15s so the
/// admin panel auto-discovers this machine and IP changes are self-healing
/// (spec §2). Best-effort; failures are non-fatal.
/// </summary>
public sealed class UdpAnnouncer
{
    private readonly ILogger _log;
    private readonly int _announcePort;
    private readonly int _commandPort;
    private readonly Func<bool> _studentModeGetter;

    public UdpAnnouncer(ILogger log, int announcePort, int commandPort, Func<bool> studentModeGetter)
    {
        _log = log;
        _announcePort = announcePort;
        _commandPort = commandPort;
        _studentModeGetter = studentModeGetter;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var udp = new UdpClient { EnableBroadcast = true };
        var endpoint = new IPEndPoint(IPAddress.Broadcast, _announcePort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var packet = new AnnouncePacket
                {
                    MachineId = MachineIdentity.MachineId,
                    Name = MachineIdentity.Name,
                    Ip = MachineIdentity.LocalIPv4(),
                    Version = MachineIdentity.Version,
                    Port = _commandPort,
                    StudentMode = _studentModeGetter(),
                };
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet));
                await udp.SendAsync(bytes, bytes.Length, endpoint);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "UDP announce failed (non-fatal).");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
