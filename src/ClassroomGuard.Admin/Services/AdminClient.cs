using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Security;
namespace ClassroomGuard.Admin.Services;
public interface IAdminClient { Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000); }
public sealed class AdminClient : IAdminClient
{
    private readonly ClassroomKey _key;
    private readonly IReadOnlyDictionary<string,string> _pins;
    public AdminClient(ClassroomKey key,IReadOnlyDictionary<string,string> pins){_key=key;_pins=new Dictionary<string,string>(pins);}
    public async Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000)
    {
        if(!_pins.TryGetValue(machine.MachineId,out var pin))return NetResponse.Fail("Device not enrolled. Import its certificate enrollment file in Settings.");
        try
        {
            using var timeout=new CancellationTokenSource(timeoutMs);var ct=timeout.Token;
            using var tcp=new TcpClient();await tcp.ConnectAsync(machine.Ip,machine.Port,ct);
            using var ssl=new SslStream(tcp.GetStream(),false,(_,certificate,_,_)=>certificate is not null && DeviceIdentity.Fingerprint(certificate).Equals(pin,StringComparison.OrdinalIgnoreCase));
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="ClassroomGuardAgent",EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},ct);
            using var reader=new StreamReader(ssl,leaveOpen:true);using var writer=new StreamWriter(ssl,leaveOpen:true){AutoFlush=true};
            var hello=JsonSerializer.Deserialize<NetResponse>(await Wire.ReadAsync(reader,ct)??"");
            if(hello?.Nonce is null)throw new IOException("No challenge.");
            await Wire.WriteAsync(writer,JsonSerializer.Serialize(new NetRequest{Command=NetCommand.Auth,AuthHmac=_key.Hmac(hello.Nonce)}),ct);
            var auth=JsonSerializer.Deserialize<NetResponse>(await Wire.ReadAsync(reader,ct)??"");
            if(auth?.Ok!=true)throw new UnauthorizedAccessException("Classroom key rejected.");
            await Wire.WriteAsync(writer,JsonSerializer.Serialize(request),ct);
            return JsonSerializer.Deserialize<NetResponse>(await Wire.ReadAsync(reader,ct)??"")??NetResponse.Fail("Empty response.");
        }
        catch(OperationCanceledException){return NetResponse.Fail("Request timed out.");}
        catch(Exception ex){return NetResponse.Fail(ex.Message);}
    }
}
