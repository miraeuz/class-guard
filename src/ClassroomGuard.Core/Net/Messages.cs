using ClassroomGuard.Core.Models;

namespace ClassroomGuard.Core.Net;

/// <summary>UDP announce packet broadcast by each agent (spec §2 discovery).</summary>
public sealed class AnnouncePacket
{
    public string MachineId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Version { get; set; } = "";
    public int Port { get; set; }
    public bool StudentMode { get; set; }
}

public enum NetCommand
{
    Hello,           // server -> client: carries a nonce to sign
    Auth,            // client -> server: HMAC(psk, nonce)
    GetStatus,
    SetState,
    EnableStudent,
    DisableStudent,  // requires teacher password OR a valid unlock code
    Uninstall,       // requires a valid unlock code (TOTP/Telegram)
    Ping,
    LockScreen,
    UnlockScreen,
}

public sealed class NetRequest
{
    public NetCommand Command { get; set; }
    public string? AuthHmac { get; set; }
    public EnforcementState? State { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Code { get; set; }
}

public sealed class NetResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public string? Nonce { get; set; }
    /// <summary>Hello only: HMAC(classroom key, "agent-cert:" + SHA-256 of the agent TLS certificate).
    /// Proves the certificate belongs to a holder of the classroom key, so no manual pinning is needed.</summary>
    public string? CertProof { get; set; }
    public EnforcementState? State { get; set; }
    public MachineInfo? Machine { get; set; }
    public AgentHealth? Health { get; set; }

    public static NetResponse Fail(string m) => new() { Ok = false, Message = m };
    public static NetResponse Success(string m = "OK") => new() { Ok = true, Message = m };
}

/// <summary>Admin-side view of a discovered machine.</summary>
public sealed class MachineInfo
{
    public string MachineId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; }
    public string Version { get; set; } = "";
    public bool StudentMode { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public EnforcementState? ReportedState { get; set; }
    public bool Verified { get; set; }
    public bool IdentityConflict { get; set; }
    public DateTimeOffset LastVerified { get; set; }
    public AgentHealth? Health { get; set; }

    /// <summary>online / offline / offline-in-student-mode (the "worth attention" case).</summary>
    public string StatusText(TimeSpan offlineAfter)
    {
        var age = DateTimeOffset.UtcNow - LastSeen;
        if (age <= offlineAfter) return "online";
        return StudentMode ? "offline (was locked!)" : "offline";
    }
}
