using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
namespace ClassroomGuard.Core.Security;
public sealed class DeviceEnrollment
{
    public string MachineId {get;set;}="";
    public string Name {get;set;}="";
    public string CertificateSha256 {get;set;}="";
}
public static class DeviceIdentity
{
    public static X509Certificate2 LoadOrCreate()
    {
        var path=Path.Combine(AppPaths.DataDir,"device.pfx");
        X509Certificate2 cert;
        if(File.Exists(path))cert=new X509Certificate2(File.ReadAllBytes(path),(string?)null,X509KeyStorageFlags.EphemeralKeySet);
        else
        {
            using var rsa=RSA.Create(3072);
            var request=new CertificateRequest("CN=ClassroomGuardAgent",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(3));
            var bytes=generated.Export(X509ContentType.Pfx);
            File.WriteAllBytes(path,bytes);
            cert=new X509Certificate2(bytes,(string?)null,X509KeyStorageFlags.EphemeralKeySet);
        }
        File.WriteAllText(Path.Combine(AppPaths.DataDir,"device-enrollment.json"),JsonSerializer.Serialize(new DeviceEnrollment{MachineId=MachineIdentity.MachineId,Name=MachineIdentity.Name,CertificateSha256=Fingerprint(cert)},new JsonSerializerOptions{WriteIndented=true}));
        return cert;
    }
    public static string Fingerprint(X509Certificate certificate)=>Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
}
