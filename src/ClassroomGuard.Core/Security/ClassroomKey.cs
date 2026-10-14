using System.Security.Cryptography;
using System.Text;

namespace ClassroomGuard.Core.Security;

/// <summary>
/// The per-classroom shared secret. Generated ONCE at classroom setup (never a
/// hardcoded default, spec §2). Used for two things:
///   * authenticating the admin to an agent (HMAC challenge over the TLS channel);
///   * deriving each machine's unique offline unlock (TOTP) seed, so a teacher who
///     holds the classroom key can compute any machine's current unlock code
///     offline, without the machine ever storing a shared static code.
/// </summary>
public sealed class ClassroomKey
{
    private readonly byte[] _secret;

    public string Base64 => Convert.ToBase64String(_secret);

    private ClassroomKey(byte[] secret) => _secret = secret;

    public static ClassroomKey Generate() => new(RandomNumberGenerator.GetBytes(32));

    public static ClassroomKey FromBase64(string b64)
    {
        var bytes=Convert.FromBase64String(b64);
        if(bytes.Length!=32) throw new ArgumentException("Classroom key must contain 32 random bytes.");
        return new(bytes);
    }

    /// <summary>HMAC-SHA256 of arbitrary data with the classroom secret (hex).</summary>
    public string Hmac(string data)
    {
        using var h = new HMACSHA256(_secret);
        var mac = h.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(mac);
    }

    public bool VerifyHmac(string data, string providedHex)
    {
        var expected = Hmac(data);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(providedHex));
    }

    /// <summary>
    /// Per-machine TOTP seed = HMAC(secret, "totp:" + machineId). Unique per
    /// machine, but re-derivable by anyone holding the classroom key — so no
    /// per-machine secret ever needs to be stored or transported.
    /// </summary>
    public byte[] TotpSeed(string machineId)
    {
        using var h = new HMACSHA256(_secret);
        return h.ComputeHash(Encoding.UTF8.GetBytes("totp:" + machineId));
    }
}
