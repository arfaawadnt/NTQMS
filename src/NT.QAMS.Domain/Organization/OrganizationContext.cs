using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Organization;

public enum InterestedPartyStatus { Active, Archived }

/// <summary>
/// Interested-party register entry (ISO 9001 §4.2 / ISO 17025 §4.1.4): who has
/// a stake in the lab, what they need and expect, and which of their
/// requirements the QMS must satisfy. A living register — entries are updated
/// in place (field-level audit captures every change) and archived, never
/// deleted.
/// </summary>
public sealed class InterestedParty : AggregateRoot, ITenantScoped
{
    private InterestedParty()
    {
        PartyRef = null!;
        Name = null!;
        Category = null!;
        NeedsAndExpectations = null!;
    }

    public Guid TenantId { get; set; }
    public string PartyRef { get; private set; }
    public string Name { get; private set; }
    /// <summary>LOV-managed, e.g. Customer, Regulator, Accreditation body, Staff, Supplier, Owner.</summary>
    public string Category { get; private set; }
    public string NeedsAndExpectations { get; private set; }
    /// <summary>The subset of needs the QMS commits to satisfying (requirements).</summary>
    public string? RelevantRequirements { get; private set; }
    public DateOnly ReviewedOn { get; private set; }
    public InterestedPartyStatus Status { get; private set; }

    public static InterestedParty Register(
        string partyRef, string name, string category,
        string needsAndExpectations, string? relevantRequirements, DateOnly reviewedOn)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
        {
            throw new DomainException("IP-001", "The party name and category are required.");
        }

        if (string.IsNullOrWhiteSpace(needsAndExpectations))
        {
            throw new DomainException("IP-002", "The needs and expectations are the point of the register — they are required.");
        }

        return new InterestedParty
        {
            PartyRef = partyRef,
            Name = name.Trim(),
            Category = category.Trim(),
            NeedsAndExpectations = needsAndExpectations.Trim(),
            RelevantRequirements = string.IsNullOrWhiteSpace(relevantRequirements) ? null : relevantRequirements.Trim(),
            ReviewedOn = reviewedOn,
            Status = InterestedPartyStatus.Active,
        };
    }

    /// <summary>In-place revision of a living register entry (field-level audit records the diff).</summary>
    public void Revise(
        string name, string category, string needsAndExpectations,
        string? relevantRequirements, DateOnly reviewedOn)
    {
        if (Status != InterestedPartyStatus.Active)
        {
            throw new InvalidStateTransitionException("IP-010", "An archived entry is frozen — register a new one instead.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category)
            || string.IsNullOrWhiteSpace(needsAndExpectations))
        {
            throw new DomainException("IP-001", "The party name, category, and needs are required.");
        }

        Name = name.Trim();
        Category = category.Trim();
        NeedsAndExpectations = needsAndExpectations.Trim();
        RelevantRequirements = string.IsNullOrWhiteSpace(relevantRequirements) ? null : relevantRequirements.Trim();
        ReviewedOn = reviewedOn;
    }

    public void Archive()
    {
        if (Status == InterestedPartyStatus.Archived)
        {
            throw new InvalidStateTransitionException("IP-011", "The entry is already archived.");
        }

        Status = InterestedPartyStatus.Archived;
    }
}

public enum ContextIssueType { Internal, External }

public enum ContextIssueStatus { Active, Closed }

/// <summary>
/// Internal/external context issue (ISO 9001 §4.1): a condition that affects
/// the QMS's ability to achieve its intended results, with its assessed
/// impact. Issues are revised in place while active and closed with a
/// resolution; a linked risk records where the issue entered the risk
/// register.
/// </summary>
public sealed class ContextIssue : AggregateRoot, ITenantScoped
{
    private ContextIssue()
    {
        IssueRef = null!;
        Category = null!;
        Description = null!;
        Impact = null!;
    }

    public Guid TenantId { get; set; }
    public string IssueRef { get; private set; }
    public ContextIssueType Type { get; private set; }
    /// <summary>LOV-managed lens, e.g. Strength/Weakness/Opportunity/Threat or PESTLE category.</summary>
    public string Category { get; private set; }
    public string Description { get; private set; }
    public string Impact { get; private set; }
    /// <summary>Set when the issue was carried into the risk register.</summary>
    public Guid? LinkedRiskId { get; private set; }
    public ContextIssueStatus Status { get; private set; }
    public string? Resolution { get; private set; }

    public static ContextIssue Register(
        string issueRef, ContextIssueType type, string category, string description, string impact)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("CTX-001", "The issue category and description are required.");
        }

        if (string.IsNullOrWhiteSpace(impact))
        {
            throw new DomainException("CTX-002", "The assessed impact on the QMS is required.");
        }

        return new ContextIssue
        {
            IssueRef = issueRef,
            Type = type,
            Category = category.Trim(),
            Description = description.Trim(),
            Impact = impact.Trim(),
            Status = ContextIssueStatus.Active,
        };
    }

    /// <summary>In-place revision of a living register entry (field-level audit records the diff).</summary>
    public void Revise(ContextIssueType type, string category, string description, string impact)
    {
        RequireActive();
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description)
            || string.IsNullOrWhiteSpace(impact))
        {
            throw new DomainException("CTX-001", "The issue category, description, and impact are required.");
        }

        Type = type;
        Category = category.Trim();
        Description = description.Trim();
        Impact = impact.Trim();
    }

    public void LinkRisk(Guid riskId)
    {
        RequireActive();
        LinkedRiskId = riskId;
    }

    public void Close(string resolution)
    {
        RequireActive();
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new DomainException("CTX-003", "A resolution is required to close a context issue.");
        }

        Status = ContextIssueStatus.Closed;
        Resolution = resolution.Trim();
    }

    private void RequireActive()
    {
        if (Status != ContextIssueStatus.Active)
        {
            throw new InvalidStateTransitionException("CTX-010", "The issue is closed and frozen.");
        }
    }
}
