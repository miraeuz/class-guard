using ClassroomGuard.Core.Models;

namespace ClassroomGuard.Core.Enforcement;

/// <summary>
/// One reversible restriction mechanism. Every enforcer must guarantee that
/// <see cref="Revert"/> returns the system to exactly its stock, unrestricted
/// state — no orphaned entries, rules, or keys (spec §1.2). <see cref="Apply"/>
/// must be idempotent: applying twice equals applying once.
/// </summary>
public interface IEnforcer
{
    string Name { get; }

    /// <summary>Apply this enforcer according to the given intended state.</summary>
    void Apply(EnforcementState state);

    /// <summary>Fully and cleanly remove everything this enforcer ever applied.</summary>
    void Revert();
}
