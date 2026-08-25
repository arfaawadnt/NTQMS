using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.IncidentReporting;

/// <summary>Lifecycle of a hospital incident/occurrence report (HQMS M02).</summary>
public enum IncidentStatus
{
    /// <summary>Just submitted; awaiting triage by the quality/patient-safety function.</summary>
    Reported,

    /// <summary>Classified and assigned to an owner.</summary>
    Triaged,

    /// <summary>An investigator is reconstructing the event and its contributing factors.</summary>
    UnderInvestigation,

    /// <summary>Investigation complete; awaiting quality-function review and closure.</summary>
    PendingReview,

    /// <summary>Reviewed and closed with a documented outcome.</summary>
    Closed,

    /// <summary>Rejected at triage (duplicate, not an incident, out of scope).</summary>
    Rejected,
}

/// <summary>
/// Degree of harm reached, on a recognised safety scale ordered from least to most
/// severe. The ordering is meaningful: escalation rules key off the grade, and the
/// two most severe grades are the ones that typically warrant a sentinel review.
/// </summary>
public enum HarmGrade
{
    /// <summary>Unsafe condition or near miss — no patient reached.</summary>
    NearMiss = 0,

    /// <summary>Reached the patient but caused no harm.</summary>
    NoHarm = 1,

    /// <summary>Temporary harm requiring minor intervention.</summary>
    Minor = 2,

    /// <summary>Temporary harm requiring significant intervention or prolonged stay.</summary>
    Moderate = 3,

    /// <summary>Permanent harm or intervention required to sustain life.</summary>
    Severe = 4,

    /// <summary>Death contributed to by the event.</summary>
    Death = 5,
}

/// <summary>Hospital event taxonomy — the top-level classification of an occurrence.</summary>
public enum IncidentCategory
{
    Medication,
    Fall,
    Procedural,
    Transfusion,
    Device,
    Laboratory,
    Security,
    Documentation,
    Other,
}

/// <summary>How the report entered the system, recorded for safety-culture analysis.</summary>
public enum IntakeChannel
{
    Web,
    Mobile,
    Kiosk,
    Phone,
    Paper,
}

/// <summary>A category on the fishbone/Ishikawa axis used to organise contributing factors.</summary>
public enum ContributingFactorCategory
{
    People,
    Process,
    Equipment,
    Environment,
    Materials,
    Management,
    Other,
}

/// <summary>A single identified contributing factor found during investigation.</summary>
public sealed class ContributingFactor : Entity
{
    internal ContributingFactor(ContributingFactorCategory category, string description)
    {
        Category = category;
        Description = description;
    }

    private ContributingFactor() { Description = null!; }

    /// <summary>The fishbone axis this factor sits on.</summary>
    public ContributingFactorCategory Category { get; private set; }

    /// <summary>Free-text description of the factor.</summary>
    public string Description { get; private set; }
}

/// <summary>One entry in the reconstructed timeline of the event.</summary>
public sealed class IncidentTimelineEntry : Entity
{
    internal IncidentTimelineEntry(DateTimeOffset occurredAtUtc, string note, Guid recordedBy)
    {
        OccurredAtUtc = occurredAtUtc;
        Note = note;
        RecordedBy = recordedBy;
    }

    private IncidentTimelineEntry() { Note = null!; }

    /// <summary>When the described step happened.</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>What happened at this point.</summary>
    public string Note { get; private set; }

    /// <summary>The user who recorded this entry.</summary>
    public Guid RecordedBy { get; private set; }
}

/// <summary>
/// The Incident aggregate — the canonical hospital occurrence report (HQMS M02).
/// A six-state machine (Reported → Triaged → UnderInvestigation → PendingReview →
/// Closed, plus Rejected). Invariants enforced in the aggregate: guarded
/// transitions; investigation summary required before review; segregation of
/// duties (the reporter cannot close their own incident, SOD-INC-001); anonymous
/// reports never carry a reporter identity. Two acts are Part 11 signing
/// ceremonies handled at the application boundary — declaring a sentinel event and
/// closing the record — and the aggregate re-checks their preconditions.
/// </summary>
public sealed class Incident : AggregateRoot, ITenantScoped
{
    private readonly List<ContributingFactor> _contributingFactors = [];
    private readonly List<IncidentTimelineEntry> _timeline = [];

    private Incident()
    {
        IncidentRef = null!;
        Title = null!;
        Description = null!;
    }

    /// <inheritdoc />
    public Guid TenantId { get; set; }

    /// <summary>Branch the incident is scoped to (org-scope filter), if known.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Department the incident is scoped to, if known.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Human-readable reference, e.g. <c>INC-2026-0001</c>.</summary>
    public string IncidentRef { get; private set; }

    /// <summary>Short title of the occurrence.</summary>
    public string Title { get; private set; }

    /// <summary>What happened, in the reporter's words.</summary>
    public string Description { get; private set; }

    /// <summary>Top-level event taxonomy.</summary>
    public IncidentCategory Category { get; private set; }

    /// <summary>Where the event occurred (ward, unit, room).</summary>
    public string? Location { get; private set; }

    /// <summary>When the event occurred (may predate the report).</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>How the report was submitted.</summary>
    public IntakeChannel Channel { get; private set; }

    /// <summary>Degree of harm reached.</summary>
    public HarmGrade HarmGrade { get; private set; }

    /// <summary>True once a sentinel-event determination has been signed.</summary>
    public bool IsSentinel { get; private set; }

    /// <summary>When the sentinel determination was signed, if any.</summary>
    public DateTimeOffset? SentinelDeclaredAtUtc { get; private set; }

    /// <inheritdoc cref="IncidentStatus"/>
    public IncidentStatus Status { get; private set; }

    /// <summary>The reporter, or <c>null</c> when the report was submitted anonymously.</summary>
    public Guid? ReportedBy { get; private set; }

    /// <summary>True when identity was suppressed; no reporter is stored.</summary>
    public bool IsAnonymous { get; private set; }

    /// <summary>
    /// SHA-256 (lower-case hex) of the one-time follow-up reference issued to an
    /// anonymous reporter. Only the hash is stored, so the code cannot be recovered
    /// from the record; the reporter presents the code to track their report.
    /// </summary>
    public string? AnonymousReferenceHash { get; private set; }

    /// <summary>Owner assigned at triage.</summary>
    public Guid? AssignedTo { get; private set; }

    /// <summary>Investigator assigned when investigation starts.</summary>
    public Guid? InvestigatorId { get; private set; }

    /// <summary>The investigator's contributing-factor analysis and conclusions.</summary>
    public string? InvestigationSummary { get; private set; }

    /// <summary>Reason recorded when an incident is rejected at triage.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Outcome recorded when the incident is closed.</summary>
    public string? ClosureSummary { get; private set; }

    /// <summary>
    /// The corrective-action pipeline (Nonconformance/CAPA) raised from this incident, if any.
    /// The incident is the "source" and the CAPA is the "one loop" it converges into
    /// (HQMS design principle "one loop, many sources"). Set once; never re-pointed.
    /// </summary>
    public Guid? CorrectiveActionNcId { get; private set; }

    /// <summary>The reconstructed contributing factors.</summary>
    public IReadOnlyList<ContributingFactor> ContributingFactors => _contributingFactors.AsReadOnly();

    /// <summary>The reconstructed event timeline, chronological.</summary>
    public IReadOnlyList<IncidentTimelineEntry> Timeline => _timeline.AsReadOnly();

    /// <summary>Reports an attributed incident (the reporter's identity is retained).</summary>
    public static Incident Report(
        string incidentRef, string title, string description, IncidentCategory category,
        HarmGrade harmGrade, IntakeChannel channel, DateTimeOffset occurredAtUtc, Guid reportedBy,
        string? location = null)
    {
        if (reportedBy == Guid.Empty)
        {
            throw new DomainException("INC-003", "An attributed report requires a reporter.");
        }

        var incident = Create(incidentRef, title, description, category, harmGrade, channel, occurredAtUtc, location);
        incident.ReportedBy = reportedBy;
        incident.IsAnonymous = false;
        incident.RaiseReported();
        return incident;
    }

    /// <summary>
    /// Reports an incident with the reporter's identity suppressed. No reporter is
    /// stored; a follow-up reference (supplied here already hashed) lets the reporter
    /// track the report without revealing who they are. This is the system-level
    /// non-punitive control the specification requires for occurrence reporting.
    /// </summary>
    public static Incident ReportAnonymous(
        string incidentRef, string title, string description, IncidentCategory category,
        HarmGrade harmGrade, IntakeChannel channel, DateTimeOffset occurredAtUtc, string referenceHash,
        string? location = null)
    {
        if (string.IsNullOrWhiteSpace(referenceHash))
        {
            throw new DomainException("INC-004", "An anonymous report requires a follow-up reference.");
        }

        var incident = Create(incidentRef, title, description, category, harmGrade, channel, occurredAtUtc, location);
        incident.ReportedBy = null;
        incident.IsAnonymous = true;
        incident.AnonymousReferenceHash = referenceHash;
        incident.RaiseReported();
        return incident;
    }

    private static Incident Create(
        string incidentRef, string title, string description, IncidentCategory category,
        HarmGrade harmGrade, IntakeChannel channel, DateTimeOffset occurredAtUtc, string? location)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("INC-001", "Title is required.");
        }

        if (occurredAtUtc == default)
        {
            throw new DomainException("INC-002", "The time the event occurred is required.");
        }

        return new Incident
        {
            IncidentRef = incidentRef,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = category,
            HarmGrade = harmGrade,
            Channel = channel,
            OccurredAtUtc = occurredAtUtc,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Status = IncidentStatus.Reported,
        };
    }

    private void RaiseReported()
    {
        Raise(new IncidentReported(Id, IncidentRef, Category, HarmGrade));

        // Automatic escalation rule: the two most severe harm grades escalate on
        // report so leadership is reached without waiting for triage. The notification
        // fabric decides recipients from the event; the aggregate only states the fact.
        if (HarmGrade is HarmGrade.Severe or HarmGrade.Death)
        {
            Raise(new IncidentEscalated(Id, IncidentRef, HarmGrade));
        }
    }

    /// <summary>Classifies and assigns the incident to an owner (Reported ⇒ Triaged).</summary>
    public void Triage(Guid assigneeId, IncidentCategory category)
    {
        Require(IncidentStatus.Reported, "INC-010", "triage");
        if (assigneeId == Guid.Empty)
        {
            throw new DomainException("INC-011", "A triage assignee is required.");
        }

        AssignedTo = assigneeId;
        Category = category;
        Status = IncidentStatus.Triaged;
        Raise(new IncidentTriaged(Id, IncidentRef, assigneeId));
    }

    /// <summary>Rejects the incident at triage with a documented reason (Reported ⇒ Rejected).</summary>
    public void Reject(string reason)
    {
        Require(IncidentStatus.Reported, "INC-012", "reject");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("INC-013", "A rejection reason is required.");
        }

        RejectionReason = reason.Trim();
        Status = IncidentStatus.Rejected;
        Raise(new IncidentRejected(Id, IncidentRef, RejectionReason));
    }

    /// <summary>Begins the investigation, assigning an investigator (Triaged ⇒ UnderInvestigation).</summary>
    public void StartInvestigation(Guid investigatorId)
    {
        Require(IncidentStatus.Triaged, "INC-014", "start investigation on");
        if (investigatorId == Guid.Empty)
        {
            throw new DomainException("INC-015", "An investigator is required.");
        }

        InvestigatorId = investigatorId;
        Status = IncidentStatus.UnderInvestigation;
    }

    /// <summary>Adds a contributing factor found during investigation.</summary>
    public void AddContributingFactor(ContributingFactorCategory category, string description)
    {
        Require(IncidentStatus.UnderInvestigation, "INC-016", "add a contributing factor to");
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("INC-017", "A contributing-factor description is required.");
        }

        _contributingFactors.Add(new ContributingFactor(category, description.Trim()));
    }

    /// <summary>Adds a timeline entry while triaging or investigating the event.</summary>
    public void AddTimelineEntry(DateTimeOffset occurredAtUtc, string note, Guid recordedBy)
    {
        if (Status is not (IncidentStatus.Triaged or IncidentStatus.UnderInvestigation))
        {
            throw new InvalidStateTransitionException(
                "INC-018", $"Cannot add a timeline entry to an incident in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("INC-019", "A timeline note is required.");
        }

        _timeline.Add(new IncidentTimelineEntry(occurredAtUtc, note.Trim(), recordedBy));
    }

    /// <summary>Records the investigator's analysis (editable while UnderInvestigation).</summary>
    public void RecordInvestigationSummary(string summary)
    {
        Require(IncidentStatus.UnderInvestigation, "INC-020", "record an investigation summary on");
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new DomainException("INC-021", "An investigation summary is required.");
        }

        InvestigationSummary = summary.Trim();
    }

    /// <summary>Submits the completed investigation for review (UnderInvestigation ⇒ PendingReview).</summary>
    public void SubmitForReview()
    {
        Require(IncidentStatus.UnderInvestigation, "INC-022", "submit for review");
        if (string.IsNullOrWhiteSpace(InvestigationSummary))
        {
            throw new DomainException("INC-023", "An investigation summary is required before review.");
        }

        Status = IncidentStatus.PendingReview;
    }

    /// <summary>
    /// Closes the incident with a documented outcome (PendingReview ⇒ Closed). A Part 11
    /// signing ceremony at the application boundary; the aggregate enforces the state gate
    /// and segregation of duties — the reporter of an attributed incident cannot close it.
    /// </summary>
    public void Close(string closureSummary, Guid actorId)
    {
        Require(IncidentStatus.PendingReview, "INC-024", "close");
        if (string.IsNullOrWhiteSpace(closureSummary))
        {
            throw new DomainException("INC-025", "A closure summary is required.");
        }

        if (ReportedBy is { } reporter && reporter == actorId)
        {
            throw new DomainException(
                "SOD-INC-001", "Segregation of duties: the reporter cannot close their own incident.");
        }

        ClosureSummary = closureSummary.Trim();
        Status = IncidentStatus.Closed;
        Raise(new IncidentClosed(Id, IncidentRef, actorId));
    }

    /// <summary>
    /// Records a signed sentinel-event determination. Allowed in any active state (a
    /// sentinel can be recognised at report, triage or during investigation) but never
    /// on a closed or rejected record. A Part 11 signing ceremony at the application
    /// boundary; the event drives the immediate executive-notification protocol.
    /// </summary>
    public void DeclareSentinel(Guid actorId, DateTimeOffset atUtc)
    {
        if (Status is IncidentStatus.Closed or IncidentStatus.Rejected)
        {
            throw new InvalidStateTransitionException(
                "INC-026", $"Cannot declare a sentinel event on an incident in state {Status}.");
        }

        if (IsSentinel)
        {
            throw new DomainException("INC-027", "This incident is already flagged as a sentinel event.");
        }

        IsSentinel = true;
        SentinelDeclaredAtUtc = atUtc;
        Raise(new SentinelDeclared(Id, IncidentRef, actorId));
    }

    /// <summary>
    /// Back-links the corrective-action record (Nonconformance/CAPA) raised from this
    /// incident. Idempotent (first link wins), so a retried escalation cannot open a
    /// second parallel loop. Not permitted on a rejected incident — a rejected report
    /// is not a finding.
    /// </summary>
    public void LinkCorrectiveAction(Guid ncId)
    {
        if (Status == IncidentStatus.Rejected)
        {
            throw new InvalidStateTransitionException(
                "INC-030", "Cannot raise a corrective action from a rejected incident.");
        }

        if (ncId == Guid.Empty)
        {
            throw new DomainException("INC-031", "A corrective-action reference is required.");
        }

        CorrectiveActionNcId ??= ncId;
    }

    private void Require(IncidentStatus expected, string code, string action)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} an incident in state {Status}.");
        }
    }
}

public sealed record IncidentReported(Guid IncidentId, string IncidentRef, IncidentCategory Category, HarmGrade HarmGrade) : DomainEvent;
public sealed record IncidentEscalated(Guid IncidentId, string IncidentRef, HarmGrade HarmGrade) : DomainEvent;
public sealed record IncidentTriaged(Guid IncidentId, string IncidentRef, Guid AssigneeId) : DomainEvent;
public sealed record IncidentRejected(Guid IncidentId, string IncidentRef, string Reason) : DomainEvent;
public sealed record SentinelDeclared(Guid IncidentId, string IncidentRef, Guid DeclaredBy) : DomainEvent;
public sealed record IncidentClosed(Guid IncidentId, string IncidentRef, Guid ClosedBy) : DomainEvent;
