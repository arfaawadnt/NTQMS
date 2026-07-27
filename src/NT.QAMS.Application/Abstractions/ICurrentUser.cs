namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// The authenticated actor for the current operation. Identity & Access (Phase 1)
/// supplies the real implementation; audit stamping and privileged operations
/// depend on it from day one.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? DisplayName { get; }
    bool IsAuthenticated { get; }

    /// <summary>The actor's role from the validated token; null when anonymous (CQRS-003).</summary>
    Domain.IdentityAccess.UserRole? Role { get; }
}
