using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AuditManagement;

public enum AuditStatus { Scheduled, InProgress, SignedOff }

public enum AuditType { Internal, ExternalHosted }

public enum ChecklistVerdict { Unanswered, Conform, Ofi, NonConform }

public enum FindingGrade { Ofi, MinorNc, MajorNc }

public sealed class AuditChecklistItem : Entity
{
    internal AuditChecklistItem(string isoClause, string question)
    {
        IsoClause = isoClause;
        Question = question;
        Verdict = ChecklistVerdict.Unanswered;
    }

    private AuditChecklistItem() { IsoClause = null!; Question = null!; }

    public string IsoClause { get; private set; }
    public string Question { get; private set; }
    public ChecklistVerdict Verdict { get; internal set; }
    public string? Evidence { get; internal set; }
}

public sealed class AuditFinding : Entity
{
    internal AuditFinding(FindingGrade grade, string description)
    {
        Grade = grade;
        Description = description;
    }

    private AuditFinding() { Description = null!; }

    public FindingGrade Grade { get; private set; }
    public string Description { get; private set; }
    /// <summary>Filled by the cross-module saga once Improvement confirms the NC.</summary>
    public Guid? NcId { get; internal set; }
}

/// <summary>
/// Internal audit execution. Invariants: checklist fully answered and every
/// NC-graded finding acknowledged with its Nonconformance before sign-off;
/// the record is immutable after sign-off. The finding→NC guarantee is the
/// event loop: FindingRaised → Improvement raises the NC → AcknowledgeFindingNc.
/// </summary>
public sealed class Audit : AggregateRoot, ITenantScoped
{
    private readonly List<AuditChecklistItem> _checklist = [];
    private readonly List<AuditFinding> _findings = [];

    private Audit()
    {
        AuditRef = null!;
        Title = null!;
    }

    public Guid TenantId { get; set; }
    public string AuditRef { get; private set; }
    public string Title { get; private set; }
    public AuditType Type { get; private set; }
    public Guid LeadAuditorId { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    public AuditStatus Status { get; private set; }
    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<AuditChecklistItem> Checklist => _checklist.AsReadOnly();
    public IReadOnlyList<AuditFinding> Findings => _findings.AsReadOnly();

    public static Audit Schedule(
        string auditRef, string title, AuditType type, Guid leadAuditorId, DateOnly plannedDate)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("AUD-001", "Audit title is required.");
        }

        var audit = new Audit
        {
            AuditRef = auditRef,
            Title = title.Trim(),
            Type = type,
            LeadAuditorId = leadAuditorId,
            PlannedDate = plannedDate,
            Status = AuditStatus.Scheduled,
        };
        audit.Raise(new AuditScheduled(audit.Id, auditRef, title.Trim(), leadAuditorId, plannedDate));
        return audit;
    }

    public Guid AddChecklistItem(string isoClause, string question)
    {
        RequireNotSignedOff();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new DomainException("AUD-002", "Checklist question is required.");
        }

        var item = new AuditChecklistItem(isoClause?.Trim() ?? string.Empty, question.Trim());
        _checklist.Add(item);
        return item.Id;
    }

    public void Start()
    {
        if (Status != AuditStatus.Scheduled)
        {
            throw new InvalidStateTransitionException("AUD-010", $"Cannot start an audit in state {Status}.");
        }

        if (_checklist.Count == 0)
        {
            throw new DomainException("AUD-011", "An audit needs at least one checklist item before it starts.");
        }

        Status = AuditStatus.InProgress;
    }

    public void AnswerChecklistItem(Guid itemId, ChecklistVerdict verdict, string? evidence)
    {
        RequireInProgress("answer checklist items");
        if (verdict == ChecklistVerdict.Unanswered)
        {
            throw new DomainException("AUD-012", "A verdict is required (Conform, Ofi or NonConform).");
        }

        var item = _checklist.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("AUD-013", "Checklist item not found on this audit.");

        item.Verdict = verdict;
        item.Evidence = string.IsNullOrWhiteSpace(evidence) ? null : evidence.Trim();
    }

    public Guid RaiseFinding(FindingGrade grade, string description, Guid raisedBy)
    {
        RequireInProgress("raise findings");
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("AUD-014", "Finding description is required.");
        }

        var finding = new AuditFinding(grade, description.Trim());
        _findings.Add(finding);
        Raise(new FindingRaised(
            Id, AuditRef, finding.Id, grade, finding.Description, TenantId, raisedBy));
        return finding.Id;
    }

    public void AcknowledgeFindingNc(Guid findingId, Guid ncId)
    {
        RequireNotSignedOff();
        var finding = _findings.FirstOrDefault(f => f.Id == findingId)
            ?? throw new DomainException("AUD-015", "Finding not found on this audit.");

        if (finding.Grade == FindingGrade.Ofi)
        {
            throw new DomainException("AUD-016", "OFI findings do not carry nonconformances.");
        }

        finding.NcId = ncId;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        RequireInProgress("sign off");

        if (_checklist.Any(i => i.Verdict == ChecklistVerdict.Unanswered))
        {
            throw new DomainException("AUD-017", "All checklist items must be answered before sign-off.");
        }

        var unacknowledged = _findings.Count(f => f.Grade != FindingGrade.Ofi && f.NcId is null);
        if (unacknowledged > 0)
        {
            throw new DomainException(
                "AUD-018",
                $"{unacknowledged} NC-graded finding(s) await their nonconformance before sign-off.");
        }

        Status = AuditStatus.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new AuditSignedOff(Id, AuditRef, actorId));
    }

    private void RequireInProgress(string action)
    {
        if (Status != AuditStatus.InProgress)
        {
            throw new InvalidStateTransitionException("AUD-019", $"Cannot {action}: audit is {Status}.");
        }
    }

    private void RequireNotSignedOff()
    {
        if (Status == AuditStatus.SignedOff)
        {
            throw new InvalidStateTransitionException("AUD-020", "A signed-off audit is immutable.");
        }
    }
}

public sealed record AuditScheduled(
    Guid AuditId, string AuditRef, string Title, Guid LeadAuditorId, DateOnly PlannedDate) : DomainEvent;

/// <summary>Carries tenant + actor so the cross-module policy can run in a background scope.</summary>
public sealed record FindingRaised(
    Guid AuditId, string AuditRef, Guid FindingId, FindingGrade Grade,
    string Description, Guid TenantId, Guid RaisedBy) : DomainEvent;

public sealed record AuditSignedOff(Guid AuditId, string AuditRef, Guid SignedOffBy) : DomainEvent;
