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
/// <summary>
/// Trust model (no manual enrollment): the agent's hello carries HMAC(classroom key, its TLS
/// certificate fingerprint). TLS proves the agent owns that certificate's private key; the HMAC
/// proves the certificate was issued by a holder of the classroom key. The proof is checked
/// BEFORE the teacher console sends its own authentication or any command, so a rogue
/// listener on the network learns nothing and receives no commands.
/// </summary>
public sealed class AdminClient : IAdminClient
{
    private readonly ClassroomKey _key;
    public AdminClient(ClassroomKey key){_key=key;}
    public async Task<NetResponse> SendAsync(MachineInfo machine,NetRequest request,int timeoutMs=10000)
    {
        if(machine.IdentityConflict)return NetResponse.Fail("Duplicate identity: two PCs report the same ID. Run scripts\repair-device-identity.ps1 on the affected PCs before management.");
        try
        {
            using var timeout=new CancellationTokenSource(timeoutMs);var ct=timeout.Token;
            using var tcp=new TcpClient();await tcp.ConnectAsync(machine.Ip,machine.Port,ct);
            string? fingerprint=null;
            using var ssl=new SslStream(tcp.GetStream(),false,(_,certificate,_,_)=>{if(certificate is null)return false;fingerprint=DeviceIdentity.Fingerprint(certificate);return true;});
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="ClassroomGuardAgent",EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},ct);
            if(fingerprint is null)throw new AuthenticationException("No agent certificate.");
            using var reader=new StreamReader(ssl,leaveOpen:true);using var writer=new StreamWriter(ssl,leaveOpen:true){AutoFlush=true};
            var hello=JsonSerializer.Deserialize<NetResponse>(await Wire.ReadAsync(reader,ct)??"");
            if(hello?.Nonce is null)throw new IOException("No challenge.");
            if(hello.CertProof is null || !_key.VerifyHmac(DeviceIdentity.ProofInput(fingerprint),hello.CertProof))
                throw new AuthenticationException("Device is not part of this classroom (different classroom password or not a ClassroomGuard agent).");
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
