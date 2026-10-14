using System.Security.Cryptography;
using ClassroomGuard.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace ClassroomGuard.Core.Security;

/// <summary>
/// Teacher credentials for the local unlock path (spec §4.1). Passwords are
/// hashed with PBKDF2-SHA256 (built into .NET, no external dependency).
///
/// NO SHIPPED STATIC PASSWORD (spec §4.2): on first run a RANDOM bootstrap
/// password is generated, written once to a protected bootstrap file, and the
/// account is flagged MustChange. It stops working the moment the teacher sets a
/// real password. This is deliberately the opposite of a fixed "q1w2e3r4"-style
/// code, which becomes public across a lab within days.
/// </summary>
public sealed class CredentialStore
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public sealed class Record
    {
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public bool MustChange { get; set; }
    }

    public sealed class Data
    {
        public Dictionary<string, Record> Users { get; set; } = new();
    }

    private readonly AtomicJsonStore<Data> _store;
    private readonly string _bootstrapPath;
    private readonly ILogger _log;

    public CredentialStore(string usersPath, string bootstrapPath, ILogger log)
    {
        _store = new AtomicJsonStore<Data>(usersPath, () => new Data(), log);
        _bootstrapPath = bootstrapPath;
        _log = log;
    }

    /// <summary>Create the default teacher account with a random password on first run.</summary>
    public void EnsureBootstrapUser(string username = "teacher")
    {
        var data = _store.Load();
        if (data.Users.ContainsKey(username)) return;

        var password = GenerateBootstrapPassword();
        data.Users[username] = Create(password, mustChange: true);
        _store.Save(data);

        try
        {
            File.WriteAllText(_bootstrapPath,
                $"ClassroomGuard first-run credentials\r\n" +
                $"username: {username}\r\npassword: {password}\r\n" +
                $"You MUST change this on first login; it then stops working.\r\n");
            _log.LogWarning("Bootstrap credentials written to {Path}. Change on first login.", _bootstrapPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not write bootstrap credential file.");
        }
    }

    public bool Verify(string username, string password, out bool mustChange)
    {
        mustChange = false;
        var data = _store.Load();
        if (!data.Users.TryGetValue(username, out var rec)) return false;

        var salt = Convert.FromBase64String(rec.Salt);
        var expected = Convert.FromBase64String(rec.Hash);
        var actual = Pbkdf2(password, salt);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return false;

        mustChange = rec.MustChange;
        return true;
    }

    public bool ChangePassword(string username, string oldPassword, string newPassword)
    {
        if (newPassword.Length < 12 || newPassword.Length > 256 || !Verify(username, oldPassword, out _)) return false;
        var data = _store.Load();
        if (!data.Users.TryGetValue(username, out _)) return false;
        data.Users[username] = Create(newPassword, mustChange: false);
        _store.Save(data);
        try { File.Delete(_bootstrapPath); } catch (IOException) { }
        _log.LogInformation("Password changed for {User}.", username);
        return true;
    }

    // --- helpers ------------------------------------------------------------

    private static Record Create(string password, bool mustChange)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Pbkdf2(password, salt);
        return new Record
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            MustChange = mustChange,
        };
    }

    private static byte[] Pbkdf2(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

    private static string GenerateBootstrapPassword()
    {
        // 12 URL-safe chars from a CSPRNG.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return string.Concat(bytes.Select(b => alphabet[b % alphabet.Length]));
    }
}
