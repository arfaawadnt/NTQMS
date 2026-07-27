using NT.QAMS.Domain.IdentityAccess;

namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// CQRS-003: the authorization policy markers every command MUST carry — the
/// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> denies
/// any command without one (fail closed), and an architecture test makes the
/// omission a compile-gate failure rather than a runtime surprise.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public abstract class CommandPolicyAttribute : Attribute;

/// <summary>
/// Any authenticated INTERNAL actor — every role except the read-only
/// <see cref="UserRole.ExternalAuditor"/>. The default policy for write
/// commands: auditors read the quality ledger, they never mutate it.
/// </summary>
public sealed class RequireInternalActorAttribute : CommandPolicyAttribute;

/// <summary>
/// Any authenticated actor INCLUDING the external auditor — reserved for
/// self-service account security (MFA enrollment, e-signature PIN) that every
/// signed-in role must be able to perform.
/// </summary>
public sealed class RequireAuthenticatedActorAttribute : CommandPolicyAttribute;

/// <summary>Only the listed roles may execute the command.</summary>
public sealed class RequireRoleAttribute(params UserRole[] roles) : CommandPolicyAttribute
{
    public IReadOnlyList<UserRole> Roles { get; } = roles;
}

/// <summary>
/// The command runs without an authenticated actor BY DESIGN (login,
/// expired-password rotation). The handler carries its own credential checks.
/// </summary>
public sealed class AllowUnauthenticatedAttribute : CommandPolicyAttribute;
