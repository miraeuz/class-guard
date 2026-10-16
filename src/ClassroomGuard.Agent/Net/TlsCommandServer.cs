using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
using ClassroomGuard.Core;
using ClassroomGuard.Core.Net;
using ClassroomGuard.Core.Security;
using Microsoft.Extensions.Logging;
namespace ClassroomGuard.Agent.Net;
public sealed class TlsCommandServer
{
    private readonly ILogger _log;private readonly int _port;private readonly ClassroomKey _key;private readonly Func<NetRequest,NetResponse> _handler;
    private readonly SemaphoreSlim _slots=new(24,24);
    public TlsCommandServer(ILogger log,int port,ClassroomKey key,Func<NetRequest,NetResponse> handler){_log=log;_port=port;_key=key;_handler=handler;}
    public async Task RunAsync(CancellationToken ct)
    {
        using var cert=DeviceIdentity.LoadOrCreate();
        var listener=new TcpListener(IPAddress.Any,_port);listener.Start();
        try
        {
            while(!ct.IsCancellationRequested)
            {
                var client=await listener.AcceptTcpClientAsync(ct);
                if(!await _slots.WaitAsync(0,ct)){client.Dispose();continue;}
                _=SessionAsync(client,cert,ct);
            }
        }
        finally{listener.Stop();}
    }
    private async Task SessionAsync(TcpClient tcp,System.Security.Cryptography.X509Certificates.X509Certificate2 cert,CancellationToken stop)
    {
        using(tcp)using(var ssl=new SslStream(tcp.GetStream(),false))using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(12));var ct=deadline.Token;
            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=cert,ClientCertificateRequired=false,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},ct);
                using var reader=new StreamReader(ssl,leaveOpen:true);using var writer=new StreamWriter(ssl,leaveOpen:true){AutoFlush=true};
                var nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                await Wire.WriteAsync(writer,JsonSerializer.Serialize(new NetResponse{Ok=true,Nonce=nonce,CertProof=_key.Hmac(DeviceIdentity.ProofInput(DeviceIdentity.Fingerprint(cert)))}),ct);
                var auth=JsonSerializer.Deserialize<NetRequest>(await Wire.ReadAsync(reader,ct)??"");
                if(auth?.Command!=NetCommand.Auth || auth.AuthHmac is null || !_key.VerifyHmac(nonce,auth.AuthHmac))throw new UnauthorizedAccessException("Authentication failed.");
                await Wire.WriteAsync(writer,JsonSerializer.Serialize(NetResponse.Success()),ct);
                var req=JsonSerializer.Deserialize<NetRequest>(await Wire.ReadAsync(reader,ct)??"")??throw new InvalidDataException();
                NetResponse response;
                try{response=_handler(req);}catch(Exception ex){response=NetResponse.Fail(ex.Message);}
                await Wire.WriteAsync(writer,JsonSerializer.Serialize(response),ct);
            }
            catch(Exception ex){_log.LogDebug(ex,"TLS session ended");}
            finally{_slots.Release();}
        }
    }
}
