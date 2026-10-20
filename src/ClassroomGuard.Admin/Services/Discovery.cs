using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClassroomGuard.Core.Net;

namespace ClassroomGuard.Admin.Services;

/// <summary>
/// Listens for untrusted UDP announcements. Each observed endpoint remains visible;
/// duplicate identities are flagged by the catalog instead of overwriting PCs.
/// </summary>
public sealed class Discovery : IDisposable
{
    private readonly int _port;
    private readonly DiscoveryCatalog _catalog = new();
    public int BoundPort => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;
    public string LastError { get; private set; } = "";
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;

    public Discovery(int announcePort) => _port = announcePort;

    public IReadOnlyCollection<MachineInfo> Machines => _catalog.Snapshot(DateTimeOffset.UtcNow);

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
                if(result.Buffer.Length>4096)continue;
                var json = Encoding.UTF8.GetString(result.Buffer);
                var pkt = JsonSerializer.Deserialize<AnnouncePacket>(json);
                _catalog.Observe(pkt,result.RemoteEndPoint.Address.ToString(),DateTimeOffset.UtcNow);
                LastError="";
            }
            catch (OperationCanceledException) { break; }
            catch (System.Text.Json.JsonException) { /* Ignore malformed untrusted announcements. */ }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { LastError=ex.Message; await Task.Delay(250,ct).ConfigureAwait(false); }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Dispose();
    }
}
