using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.RiskGovernance;

public enum RiskStatus { Identified, Mitigating, Closed }

public sealed class MitigationAction : Entity
{
    internal MitigationAction(string description, Guid ownerId, DateOnly dueDate)
    {
        Description = description;
        OwnerId = ownerId;
        DueDate = dueDate;
    }

    private MitigationAction() { Description = null!; }

    public string Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public bool Completed { get; internal set; }
}

/// <summary>
/// Risk register entry. Likelihood and impact are explicit 1-5 assessments —
/// never defaulted (the prototype's silent RPN=9 is banned). Closure requires a
/// recorded residual assessment and completed mitigation actions; residual
/// RPN above 12 raises HighResidualRisk (dashboard alert per the SRS).
/// </summary>
public sealed class RiskItem : AggregateRoot, ITenantScoped
{
    public const int HighResidualThreshold = 12;

    private readonly List<MitigationAction> _actions = [];

    private RiskItem()
    {
        RiskRef = null!;
        Title = null!;
        Category = null!;
    }

    public Guid TenantId { get; set; }
    public string RiskRef { get; private set; }
    public string Title { get; private set; }
    public string Category { get; private set; }
    public int Likelihood { get; private set; }
    public int Impact { get; private set; }
    public int Rpn { get; private set; }
    public int? ResidualLikelihood { get; private set; }
    public int? ResidualImpact { get; private set; }
    public int? ResidualRpn { get; private set; }
    public RiskStatus Status { get; private set; }

    public IReadOnlyList<MitigationAction> Actions => _actions.AsReadOnly();

    public static RiskItem Assess(string riskRef, string title, string category, int likelihood, int impact)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("RSK-001", "Risk title is required.");
        }

        ValidateScore(likelihood, impact);

        return new RiskItem
        {
            RiskRef = riskRef,
            Title = title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Operational" : category.Trim(),
            Likelihood = likelihood,
            Impact = impact,
            Rpn = likelihood * impact,
            Status = RiskStatus.Identified,
        };
    }

    public Guid AddMitigationAction(string description, Guid ownerId, DateOnly dueDate)
    {
        RequireOpen();
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("RSK-003", "Mitigation description is required.");
        }

        var action = new MitigationAction(description.Trim(), ownerId, dueDate);
        _actions.Add(action);
        Status = RiskStatus.Mitigating;
        return action.Id;
    }

    public void CompleteMitigationAction(Guid actionId)
    {
        RequireOpen();
        var action = _actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new DomainException("RSK-004", "Mitigation action not found.");
        action.Completed = true;
    }

    public void RecordResidualAssessment(int likelihood, int impact)
    {
        RequireOpen();
        ValidateScore(likelihood, impact);

        ResidualLikelihood = likelihood;
        ResidualImpact = impact;
        ResidualRpn = likelihood * impact;

        if (ResidualRpn > HighResidualThreshold)
        {
            Raise(new HighResidualRisk(Id, RiskRef, Title, ResidualRpn.Value, TenantId));
        }
    }

    public void Close()
    {
        RequireOpen();
        if (ResidualRpn is null)
        {
            throw new DomainException("RSK-005", "A residual assessment is required before closing a risk.");
        }

        if (_actions.Any(a => !a.Completed))
        {
            throw new DomainException("RSK-006", "All mitigation actions must be completed before closure.");
        }

        Status = RiskStatus.Closed;
        Raise(new RiskClosed(Id, RiskRef, ResidualRpn.Value, TenantId));
    }

    private void RequireOpen()
    {
        if (Status == RiskStatus.Closed)
        {
            throw new InvalidStateTransitionException("RSK-007", "A closed risk is immutable.");
        }
    }

    private static void ValidateScore(int likelihood, int impact)
    {
        if (likelihood is < 1 or > 5 || impact is < 1 or > 5)
        {
            throw new DomainException("RSK-002", "Likelihood and impact must each be explicitly assessed 1-5.");
        }
    }
}

public sealed record HighResidualRisk(
    Guid RiskId, string RiskRef, string Title, int ResidualRpn, Guid TenantId) : DomainEvent;

public sealed record RiskClosed(Guid RiskId, string RiskRef, int ResidualRpn, Guid TenantId) : DomainEvent;
