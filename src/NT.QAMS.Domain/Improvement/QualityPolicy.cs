using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Improvement;

public enum QualityPolicyStatus { Draft, Active, Superseded }

/// <summary>
/// The controlled quality-policy statement (ISO 9001 §5.2 / ISO 17025 §8.2): the
/// organisation's documented commitment to quality, versioned and approved by top
/// management before it takes effect. A draft is authored, then approved by someone
/// other than its author (segregation of duties); approval activates it and the
/// previously active version is superseded, so exactly one policy is in force at a
/// time and the full history is retained. An active or superseded policy is
/// immutable — a change is a new version, never an edit in place.
/// </summary>
public sealed class QualityPolicy : AggregateRoot, ITenantScoped
{
    private QualityPolicy()
    {
        PolicyRef = null!;
        Statement = null!;
    }

    public Guid TenantId { get; set; }
    public string PolicyRef { get; private set; }
    public int Version { get; private set; }
    public string Statement { get; private set; }
    public QualityPolicyStatus Status { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public Guid? ApprovedById { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public static QualityPolicy Draft(string policyRef, int version, string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            throw new DomainException("QP-001", "A policy statement is required.");
        }

        if (version < 1)
        {
            throw new DomainException("QP-002", "The policy version must be a positive number.");
        }

        return new QualityPolicy
        {
            PolicyRef = policyRef,
            Version = version,
            Statement = statement.Trim(),
            Status = QualityPolicyStatus.Draft,
        };
    }

    /// <summary>Edit the statement while still a draft; an approved policy is immutable.</summary>
    public void ReviseDraft(string statement)
    {
        if (Status != QualityPolicyStatus.Draft)
        {
            throw new InvalidStateTransitionException("QP-012", "Only a draft policy can be edited.");
        }

        if (string.IsNullOrWhiteSpace(statement))
        {
            throw new DomainException("QP-001", "A policy statement is required.");
        }

        Statement = statement.Trim();
    }

    /// <summary>
    /// Approve and activate the draft. The approver must not be its author
    /// (SOD-QP-001, Part 11 §11.10(g)); the caller supersedes any prior active
    /// version so only one policy is ever in force.
    /// </summary>
    public void Approve(Guid approverId, DateTimeOffset at, DateOnly effectiveDate)
    {
        EnsureSignerIsNotPreparer(approverId, "SOD-QP-001");

        if (Status != QualityPolicyStatus.Draft)
        {
            throw new InvalidStateTransitionException(
                "QP-010", $"Only a draft policy can be approved (current: {Status}).");
        }

        Status = QualityPolicyStatus.Active;
        ApprovedById = approverId;
        ApprovedAtUtc = at;
        EffectiveDate = effectiveDate;
        Raise(new QualityPolicyApproved(Id, PolicyRef, Version, approverId, TenantId));
    }

    /// <summary>Retire the currently active policy when a newer version is approved.</summary>
    public void Supersede()
    {
        if (Status != QualityPolicyStatus.Active)
        {
            throw new InvalidStateTransitionException("QP-011", "Only an active policy can be superseded.");
        }

        Status = QualityPolicyStatus.Superseded;
    }
}

public sealed record QualityPolicyApproved(
    Guid PolicyId, string PolicyRef, int Version, Guid ApprovedBy, Guid TenantId) : DomainEvent;
