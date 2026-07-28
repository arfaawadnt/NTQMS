using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.IdentityAccess;

/// <summary>
/// One link in a rotating refresh-token chain (ADR-0009). The browser holds
/// the opaque token in an httpOnly cookie; the server stores only its HASH.
/// Every refresh rotates: the presented session is revoked and replaced by a
/// new one in the same <see cref="FamilyId"/>. Presenting an ALREADY-ROTATED
/// token is the classic stolen-token tell — the whole family is revoked.
/// Deliberately not tenant-scoped (mirrors <see cref="UserAccount"/>): the
/// session is bound to the user, and possession of the unguessable token is
/// the access control.
/// </summary>
public sealed class RefreshSession : Entity
{
    private RefreshSession()
    {
        TokenHash = null!;
    }

    private RefreshSession(Guid id) : base(id)
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    /// <summary>All rotations of one sign-in share a family; revocation kills the family.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>SHA-256 of the token secret — the raw secret is never stored.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>The successor session when this one was rotated (null = revoked outright).</summary>
    public Guid? ReplacedById { get; private set; }

    public bool IsLive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;

    /// <summary>Was rotated and later presented again — the reuse signal.</summary>
    public bool WasRotated => ReplacedById is not null;

    /// <param name="id">Pre-minted id — it is embedded in the opaque token before the row exists.</param>
    public static RefreshSession Start(
        Guid id, Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("AUTH-000", "A refresh session requires an id and a user.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("AUTH-000", "A refresh session requires the token hash.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("AUTH-000", "A refresh session requires a positive lifetime.");
        }

        return new RefreshSession(id)
        {
            UserId = userId,
            FamilyId = familyId == Guid.Empty ? Guid.CreateVersion7() : familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + lifetime,
        };
    }

    /// <summary>Retires this link in favour of its successor (normal rotation).</summary>
    public void Rotate(Guid replacedById, DateTimeOffset now)
    {
        if (RevokedAtUtc is not null)
        {
            throw new DomainException("AUTH-000", "A revoked session cannot rotate.");
        }

        RevokedAtUtc = now;
        ReplacedById = replacedById;
    }

    /// <summary>Kills this link outright (logout, family revocation, deactivation).</summary>
    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;
}
