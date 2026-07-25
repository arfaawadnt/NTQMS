using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Improvement;

public enum ComplaintStatus
{
    Logged, Acknowledged, Validated, Investigating, OutcomeLogged, Resolved, Closed, Invalid,
}

public enum ComplaintChannel { Phone, Email, Portal, InPerson, Letter }

/// <summary>
/// Customer complaint (ISO 17025 7.9): a deviation reported by an external
/// party, living in the Improvement context beside the NC it may spawn.
/// State machine per the domain model: Logged → Acknowledged → Validated →
/// Investigating → OutcomeLogged → Resolved → Closed, with Invalid as the
/// terminal outcome of an unjustified validation verdict. A justified verdict
/// raises <see cref="ComplaintValidated"/>, which the Improvement policy turns
/// into a Nonconformance (source = Complaint); closure is blocked while that
/// NC remains open. The confidentiality flag drives reporter-identity masking
/// at the query boundary.
/// </summary>
public sealed class Complaint : AggregateRoot, ITenantScoped, IAllocatable
{
    private Complaint()
    {
        ComplaintRef = null!;
        ComplainantName = null!;
        Subject = null!;
        Description = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string ComplaintRef { get; private set; }
    public ComplaintChannel Channel { get; private set; }
    public string ComplainantName { get; private set; }
    public string? ComplainantContact { get; private set; }
    public bool Confidential { get; private set; }
    public string Subject { get; private set; }
    public string Description { get; private set; }
    public ComplaintStatus Status { get; private set; }
    public Guid LoggedBy { get; private set; }
    public DateTimeOffset LoggedAtUtc { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public string? ValidationVerdict { get; private set; }
    public string? InvestigationOutcome { get; private set; }
    public string? Resolution { get; private set; }
    /// <summary>The NC opened by the validation saga; null until (and unless) validated as justified.</summary>
    public Guid? LinkedNcId { get; private set; }

    public static Complaint Log(
        string complaintRef, ComplaintChannel channel, string complainantName,
        string? complainantContact, bool confidential, string subject, string description,
        Guid loggedBy, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(complainantName))
        {
            throw new DomainException("CMP-001", "The complainant name is required.");
        }

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("CMP-002", "A complaint subject and description are required.");
        }

        var complaint = new Complaint
        {
            ComplaintRef = complaintRef,
            Channel = channel,
            ComplainantName = complainantName.Trim(),
            ComplainantContact = complainantContact?.Trim(),
            Confidential = confidential,
            Subject = subject.Trim(),
            Description = description.Trim(),
            Status = ComplaintStatus.Logged,
            LoggedBy = loggedBy,
            LoggedAtUtc = at,
        };
        complaint.Raise(new ComplaintLogged(complaint.Id, complaintRef, complaint.Subject, channel.ToString()));
        return complaint;
    }

    public void Acknowledge(DateTimeOffset at)
    {
        Require(ComplaintStatus.Logged, "CMP-010", "acknowledge");
        Status = ComplaintStatus.Acknowledged;
        AcknowledgedAtUtc = at;
        Raise(new ComplaintAcknowledged(Id, ComplaintRef));
    }

    /// <summary>
    /// Records the validation verdict. Justified complaints proceed to
    /// Validated and demand an NC (via <see cref="ComplaintValidated"/>);
    /// unjustified complaints terminate as Invalid with the reason recorded.
    /// </summary>
    public void RecordValidationVerdict(bool justified, string verdictReason)
    {
        Require(ComplaintStatus.Acknowledged, "CMP-011", "validate");
        if (string.IsNullOrWhiteSpace(verdictReason))
        {
            throw new DomainException("CMP-003", "A validation verdict reason is required.");
        }

        ValidationVerdict = verdictReason.Trim();
        if (justified)
        {
            Status = ComplaintStatus.Validated;
            Raise(new ComplaintValidated(Id, ComplaintRef, Subject, Description, LoggedBy, TenantId));
        }
        else
        {
            Status = ComplaintStatus.Invalid;
        }
    }

    public void StartInvestigation()
    {
        Require(ComplaintStatus.Validated, "CMP-012", "start investigating");
        Status = ComplaintStatus.Investigating;
    }

    public void LogOutcome(string outcome)
    {
        Require(ComplaintStatus.Investigating, "CMP-013", "log an outcome for");
        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new DomainException("CMP-004", "An investigation outcome is required.");
        }

        InvestigationOutcome = outcome.Trim();
        Status = ComplaintStatus.OutcomeLogged;
    }

    public void Resolve(string resolution)
    {
        Require(ComplaintStatus.OutcomeLogged, "CMP-014", "resolve");
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new DomainException("CMP-005", "A resolution is required.");
        }

        Resolution = resolution.Trim();
        Status = ComplaintStatus.Resolved;
        Raise(new ComplaintResolved(Id, ComplaintRef));
    }

    /// <summary>
    /// Closes the complaint. The linked-NC-must-be-closed gate is checked by
    /// the command handler (same database, transactional) and passed in here so
    /// the aggregate stays persistence-ignorant.
    /// </summary>
    public void Close(bool linkedNcClosed)
    {
        Require(ComplaintStatus.Resolved, "CMP-015", "close");
        if (LinkedNcId is not null && !linkedNcClosed)
        {
            throw new DomainException("CMP-020", "The linked nonconformance must be closed before the complaint.");
        }

        Status = ComplaintStatus.Closed;
        Raise(new ComplaintClosed(Id, ComplaintRef));
    }

    /// <summary>Back-links the NC opened by the validation saga (idempotent).</summary>
    public void LinkNc(Guid ncId)
    {
        LinkedNcId ??= ncId;
    }

    private void Require(ComplaintStatus expected, string code, string action)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a complaint in state {Status}.");
        }
    }
}

public sealed record ComplaintLogged(
    Guid ComplaintId, string ComplaintRef, string Subject, string Channel) : DomainEvent;

public sealed record ComplaintAcknowledged(Guid ComplaintId, string ComplaintRef) : DomainEvent;

/// <summary>Consumed by the Improvement policy to open an NC (source = Complaint); carries the tenant for the background scope.</summary>
public sealed record ComplaintValidated(
    Guid ComplaintId, string ComplaintRef, string Subject, string Description,
    Guid LoggedBy, Guid TenantId) : DomainEvent;

public sealed record ComplaintResolved(Guid ComplaintId, string ComplaintRef) : DomainEvent;

public sealed record ComplaintClosed(Guid ComplaintId, string ComplaintRef) : DomainEvent;
