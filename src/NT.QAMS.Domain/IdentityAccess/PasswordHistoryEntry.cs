namespace NT.QAMS.Domain.IdentityAccess;

/// <summary>
/// Retired password hash retained to enforce the reuse ban (Part 11 §11.300 /
/// password-policy history depth). Append-only from the application's
/// perspective; oldest rows beyond the configured depth are pruned.
/// </summary>
public sealed class PasswordHistoryEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public string PasswordHash { get; init; } = null!;
    public DateTimeOffset SetAtUtc { get; init; }
}
