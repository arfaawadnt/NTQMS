using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Competency;

/// <summary>What the person is authorized to do for the test (§6.2.6: perform, review/release, train).</summary>
public enum AuthorizationScope { Perform, ReviewAndRelease, Train }

public enum TestAuthorizationStatus { Active, Suspended, Revoked, Expired }

/// <summary>
/// One cell of the personnel authorization matrix (ISO 17025 §6.2.6 /
/// ISO 15189 §6.2): user × catalog test × scope, granted against an Authorized
/// competency record as evidence and inheriting its expiry. Suspension is
/// reversible (and applied automatically when the underlying competency
/// expires); revocation is terminal; expiry is latched by the sweep. History is
/// never deleted — a lapsed authorization stays on the record.
/// </summary>
public sealed class TestAuthorization : AggregateRoot, ITenantScoped
{
    private TestAuthorization() { }

    public Guid TenantId { get; set; }
    public Guid UserId { get; private set; }
    public Guid TestCatalogItemId { get; private set; }
    /// <summary>The competency record presented as evidence for the grant.</summary>
    public Guid CompetencyRecordId { get; private set; }
    public AuthorizationScope Scope { get; private set; }
    public Guid GrantedBy { get; private set; }
    public DateOnly GrantedOn { get; private set; }
    /// <summary>Inherited from the evidencing competency — never later than its requalification date.</summary>
    public DateOnly ExpiresOn { get; private set; }
    public TestAuthorizationStatus Status { get; private set; }
    public string? SuspensionReason { get; private set; }
    public string? RevocationReason { get; private set; }

    public static TestAuthorization Grant(
        Guid userId, Guid testCatalogItemId, Guid competencyRecordId,
        AuthorizationScope scope, Guid grantedBy, DateOnly grantedOn, DateOnly expiresOn)
    {
        if (grantedBy == userId)
        {
            throw new DomainException("SOD-AUTHZ-001", "Segregation of duties: users cannot grant their own test authorizations.");
        }

        if (expiresOn <= grantedOn)
        {
            throw new DomainException("AUTHZ-001", "The authorization expiry must fall after the grant date.");
        }

        return new TestAuthorization
        {
            UserId = userId,
            TestCatalogItemId = testCatalogItemId,
            CompetencyRecordId = competencyRecordId,
            Scope = scope,
            GrantedBy = grantedBy,
            GrantedOn = grantedOn,
            ExpiresOn = expiresOn,
            Status = TestAuthorizationStatus.Active,
        };
    }

    public void Suspend(string reason)
    {
        if (Status != TestAuthorizationStatus.Active)
        {
            throw new InvalidStateTransitionException("AUTHZ-010", $"Only an active authorization can be suspended (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("AUTHZ-011", "A suspension reason is required.");
        }

        Status = TestAuthorizationStatus.Suspended;
        SuspensionReason = reason.Trim();
    }

    /// <summary>Saga-proposed: suspend without throwing when already off the Active path.</summary>
    public void SuspendIfActive(string reason)
    {
        if (Status == TestAuthorizationStatus.Active)
        {
            Suspend(reason);
        }
    }

    public void Reinstate(DateOnly asOf)
    {
        if (Status != TestAuthorizationStatus.Suspended)
        {
            throw new InvalidStateTransitionException("AUTHZ-012", $"Only a suspended authorization can be reinstated (current: {Status}).");
        }

        if (ExpiresOn <= asOf)
        {
            throw new DomainException("AUTHZ-013", "The authorization has lapsed — grant a new one against a current competency.");
        }

        Status = TestAuthorizationStatus.Active;
        SuspensionReason = null;
    }

    public void Revoke(Guid actorId, string reason)
    {
        if (Status is TestAuthorizationStatus.Revoked or TestAuthorizationStatus.Expired)
        {
            throw new InvalidStateTransitionException("AUTHZ-014", $"A {Status} authorization cannot be revoked.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("AUTHZ-015", "A revocation reason is required.");
        }

        Status = TestAuthorizationStatus.Revoked;
        RevocationReason = reason.Trim();
        Raise(new TestAuthorizationRevoked(Id, UserId, TestCatalogItemId, actorId, RevocationReason, TenantId));
    }

    /// <summary>Sweep-proposed: past expiry → Expired (latched; suspended entries lapse too).</summary>
    public void ExpireIfDue(DateOnly asOf)
    {
        if (Status is not (TestAuthorizationStatus.Active or TestAuthorizationStatus.Suspended) || ExpiresOn > asOf)
        {
            return; // Proposal declined — not actually lapsed.
        }

        Status = TestAuthorizationStatus.Expired;
        Raise(new TestAuthorizationExpired(Id, UserId, TestCatalogItemId, TenantId));
    }
}

public sealed record TestAuthorizationExpired(
    Guid AuthorizationId, Guid UserId, Guid TestCatalogItemId, Guid TenantId) : DomainEvent;

public sealed record TestAuthorizationRevoked(
    Guid AuthorizationId, Guid UserId, Guid TestCatalogItemId, Guid ActorId, string Reason, Guid TenantId) : DomainEvent;
