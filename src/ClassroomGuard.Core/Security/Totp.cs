using System.Security.Cryptography;

namespace ClassroomGuard.Core.Security;

/// <summary>
/// RFC 6238 TOTP. Produces a 6-digit code that rotates on a fixed step (default
/// 30 seconds), verified with a +/-1 step window (~90s total) so a teacher can read
/// it out loud. This is the OFFLINE unlock fallback (spec §4.2): it needs no
/// internet, and it replaces any static code because it changes every step.
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    private const int Digits = 6;

    public static string Now(byte[] seed) => Generate(seed, DateTimeOffset.UtcNow);

    public static string Generate(byte[] seed, DateTimeOffset when)
    {
        long counter = when.ToUnixTimeSeconds() / StepSeconds;
        return Generate(seed, counter);
    }

    /// <summary>Verify a code, allowing +/- one step for clock skew / read delay.</summary>
    public static bool Verify(byte[] seed, string code)
    {
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (long c = counter - 1; c <= counter + 1; c++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(Generate(seed, c)),
                    System.Text.Encoding.ASCII.GetBytes(code.Trim())))
                return true;
        }
        return false;
    }

    private static string Generate(byte[] seed, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(seed);
        var hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        int otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }
}
