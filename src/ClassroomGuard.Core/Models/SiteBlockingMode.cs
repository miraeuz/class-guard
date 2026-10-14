namespace ClassroomGuard.Core.Models;

/// <summary>
/// How site blocking is interpreted.
/// NOTE (Phase 1 honesty): only <see cref="Blacklist"/> is fully enforceable
/// with the hosts file. <see cref="Whitelist"/> (block everything except the
/// list) cannot be expressed in the hosts file — it needs the Firewall/WFP or a
/// local DNS sinkhole, which arrives in Phase 2. In Phase 1 the coordinator logs
/// a clear warning if Whitelist is requested and falls back to no site blocking
/// rather than silently pretending to enforce it.
/// </summary>
public enum SiteBlockingMode
{
    Off = 0,
    Blacklist = 1,
    Whitelist = 2,
}
