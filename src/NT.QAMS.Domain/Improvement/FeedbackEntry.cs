using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Improvement;

public enum FeedbackType { Compliment, Suggestion, Dissatisfaction }

public enum FeedbackStatus { Logged, Reviewed, Closed, Escalated }

/// <summary>
/// General customer/user feedback beyond formal complaints (ISO 17025 §8.6.2 /
/// ISO 15189 §8.6): compliments, suggestions and dissatisfaction, with an
/// optional 1–5 satisfaction score for trend analysis. Feedback is reviewed
/// and closed with the action taken; dissatisfaction can be escalated into the
/// formal complaint workflow, which links the two records and ends this one.
/// </summary>
public sealed class FeedbackEntry : AggregateRoot, ITenantScoped, IAllocatable
{
    private FeedbackEntry()
    {
        FeedbackRef = null!;
        Source = null!;
        Channel = null!;
        Subject = null!;
        Details = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string FeedbackRef { get; private set; }
    /// <summary>Who gave it (LOV-managed), e.g. Customer, Referring physician, Staff.</summary>
    public string Source { get; private set; }
    /// <summary>How it arrived (LOV-managed), e.g. Survey, Email, Phone, Portal.</summary>
    public string Channel { get; private set; }
    public FeedbackType Type { get; private set; }
    public string Subject { get; private set; }
    public string Details { get; private set; }
    /// <summary>Optional 1–5 satisfaction score for trend analysis.</summary>
    public int? SatisfactionScore { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public Guid LoggedBy { get; private set; }
    public FeedbackStatus Status { get; private set; }
    public string? ReviewNotes { get; private set; }
    public string? ActionSummary { get; private set; }
    /// <summary>Set when dissatisfaction was escalated into the formal complaint workflow.</summary>
    public Guid? ComplaintId { get; private set; }

    public static FeedbackEntry Log(
        string feedbackRef, string source, string channel, FeedbackType type,
        string subject, string details, int? satisfactionScore, DateOnly receivedOn, Guid loggedBy)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(details))
        {
            throw new DomainException("FBK-001", "A subject and details are required.");
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(channel))
        {
            throw new DomainException("FBK-002", "The feedback source and channel are required.");
        }

        if (satisfactionScore is < 1 or > 5)
        {
            throw new DomainException("FBK-003", "The satisfaction score is on a 1–5 scale.");
        }

        return new FeedbackEntry
        {
            FeedbackRef = feedbackRef,
            Source = source.Trim(),
            Channel = channel.Trim(),
            Type = type,
            Subject = subject.Trim(),
            Details = details.Trim(),
            SatisfactionScore = satisfactionScore,
            ReceivedOn = receivedOn,
            LoggedBy = loggedBy,
            Status = FeedbackStatus.Logged,
        };
    }

    public void Review(string reviewNotes)
    {
        if (Status != FeedbackStatus.Logged)
        {
            throw new InvalidStateTransitionException("FBK-010", $"Only logged feedback can be reviewed (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reviewNotes))
        {
            throw new DomainException("FBK-011", "Review notes are required.");
        }

        Status = FeedbackStatus.Reviewed;
        ReviewNotes = reviewNotes.Trim();
    }

    public void Close(string actionSummary)
    {
        if (Status != FeedbackStatus.Reviewed)
        {
            throw new InvalidStateTransitionException("FBK-012", $"Only reviewed feedback can be closed (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(actionSummary))
        {
            throw new DomainException("FBK-013", "A summary of the action taken (or the reason none was needed) is required.");
        }

        Status = FeedbackStatus.Closed;
        ActionSummary = actionSummary.Trim();
    }

    /// <summary>Hands dissatisfaction over to the formal complaint workflow (terminal here).</summary>
    public void Escalate(Guid complaintId)
    {
        if (Type != FeedbackType.Dissatisfaction)
        {
            throw new DomainException("FBK-014", "Only dissatisfaction can be escalated to a complaint.");
        }

        if (Status is FeedbackStatus.Closed or FeedbackStatus.Escalated)
        {
            throw new InvalidStateTransitionException("FBK-015", $"{Status} feedback cannot be escalated.");
        }

        Status = FeedbackStatus.Escalated;
        ComplaintId = complaintId;
    }
}
