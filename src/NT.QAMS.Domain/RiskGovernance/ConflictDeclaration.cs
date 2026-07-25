using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.RiskGovernance;

public enum ConflictRiskLevel { Low, Medium, High }

public enum ConflictStatus { Declared, Assessed, Closed }

public enum ConflictOutcome { Accepted, Mitigated, Withdrawn }

/// <summary>
/// Impartiality / conflict-of-interest declaration (ISO 17025 §4.1 /
/// ISO 15189 §4.1): a person declares a relationship that could bias lab
/// activities; the QM assesses the impartiality risk and mitigation
/// (never the declarant themselves — SoD), and closure records the outcome.
/// High-risk assessments raise an event so impartiality threats surface in
/// notifications. Declarations are never deleted.
/// </summary>
public sealed class ConflictDeclaration : AggregateRoot, ITenantScoped
{
    private ConflictDeclaration()
    {
        ConflictRef = null!;
        Description = null!;
        RelatedParty = null!;
    }

    public Guid TenantId { get; set; }
    public string ConflictRef { get; private set; }
    /// <summary>The person with the potential conflict.</summary>
    public Guid DeclarantId { get; private set; }
    public string Description { get; private set; }
    /// <summary>The outside party involved (company, relative, competitor…).</summary>
    public string RelatedParty { get; private set; }
    public DateOnly DeclaredOn { get; private set; }
    public ConflictStatus Status { get; private set; }
    public ConflictRiskLevel? RiskLevel { get; private set; }
    public string? Mitigation { get; private set; }
    public Guid? AssessedBy { get; private set; }
    public ConflictOutcome? Outcome { get; private set; }
    public string? ClosureNote { get; private set; }

    public static ConflictDeclaration Declare(
        string conflictRef, Guid declarantId, string description, string relatedParty, DateOnly declaredOn)
    {
        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(relatedParty))
        {
            throw new DomainException("COI-001", "The conflict description and the related party are required.");
        }

        return new ConflictDeclaration
        {
            ConflictRef = conflictRef,
            DeclarantId = declarantId,
            Description = description.Trim(),
            RelatedParty = relatedParty.Trim(),
            DeclaredOn = declaredOn,
            Status = ConflictStatus.Declared,
        };
    }

    public void Assess(Guid assessorId, ConflictRiskLevel riskLevel, string mitigation)
    {
        if (Status != ConflictStatus.Declared)
        {
            throw new InvalidStateTransitionException("COI-010", $"Only a declared conflict can be assessed (current: {Status}).");
        }

        if (assessorId == DeclarantId)
        {
            throw new DomainException("SOD-COI-001", "Segregation of duties: declarants cannot assess their own conflict.");
        }

        if (string.IsNullOrWhiteSpace(mitigation))
        {
            throw new DomainException("COI-011", "A mitigation (or the justification that none is needed) is required.");
        }

        Status = ConflictStatus.Assessed;
        RiskLevel = riskLevel;
        Mitigation = mitigation.Trim();
        AssessedBy = assessorId;

        if (riskLevel == ConflictRiskLevel.High)
        {
            Raise(new HighImpartialityRiskDeclared(Id, ConflictRef, DeclarantId, RelatedParty, Mitigation, TenantId));
        }
    }

    public void Close(ConflictOutcome outcome, string closureNote)
    {
        if (Status != ConflictStatus.Assessed)
        {
            throw new InvalidStateTransitionException("COI-012", $"Only an assessed conflict can be closed (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(closureNote))
        {
            throw new DomainException("COI-013", "A closure note is required.");
        }

        Status = ConflictStatus.Closed;
        Outcome = outcome;
        ClosureNote = closureNote.Trim();
    }
}

public sealed record HighImpartialityRiskDeclared(
    Guid ConflictId, string ConflictRef, Guid DeclarantId, string RelatedParty,
    string Mitigation, Guid TenantId) : DomainEvent;
