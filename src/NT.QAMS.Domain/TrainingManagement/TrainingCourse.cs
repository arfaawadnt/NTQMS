using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.TrainingManagement;

/// <summary>The programme a course belongs to, for the compliance dashboard and filtering.</summary>
public enum TrainingCategory { Mandatory, Clinical, Safety, Orientation, Cme }

/// <summary>Lifecycle of a course in the catalogue.</summary>
public enum CourseStatus { Draft, Active, Retired }

/// <summary>
/// A reusable course in the training catalogue (HQMS M12): the definition delivered through one or
/// more <see cref="TrainingSession"/>s. A course carries its pass mark and, for recurring mandatory
/// training, a validity window after which a completion lapses and must be repeated — the basis of
/// the compliance dashboard. Draft → Active → Retired.
/// </summary>
public sealed class TrainingCourse : AggregateRoot, ITenantScoped
{
    private TrainingCourse()
    {
        CourseRef = null!;
        Title = null!;
        Description = null!;
    }

    public Guid TenantId { get; set; }
    public string CourseRef { get; private set; }
    public string Title { get; private set; }
    public TrainingCategory Category { get; private set; }
    public string Description { get; private set; }
    public decimal DurationHours { get; private set; }

    /// <summary>How long a completion stays valid, in months; null for one-off training that never lapses.</summary>
    public int? ValidityMonths { get; private set; }

    /// <summary>Post-assessment pass threshold (0–100).</summary>
    public int PassMark { get; private set; }

    public CourseStatus Status { get; private set; }

    public static TrainingCourse Define(
        string courseRef, string title, TrainingCategory category, string description,
        decimal durationHours, int? validityMonths, int passMark)
    {
        Validate(title, durationHours, validityMonths, passMark);
        return new TrainingCourse
        {
            CourseRef = courseRef,
            Title = title.Trim(),
            Category = category,
            Description = description?.Trim() ?? string.Empty,
            DurationHours = durationHours,
            ValidityMonths = validityMonths,
            PassMark = passMark,
            Status = CourseStatus.Draft,
        };
    }

    public void UpdateDetails(
        string title, TrainingCategory category, string description,
        decimal durationHours, int? validityMonths, int passMark)
    {
        if (Status != CourseStatus.Draft)
        {
            throw new InvalidStateTransitionException("CRS-010", "Only a draft course can be edited.");
        }

        Validate(title, durationHours, validityMonths, passMark);
        Title = title.Trim();
        Category = category;
        Description = description?.Trim() ?? string.Empty;
        DurationHours = durationHours;
        ValidityMonths = validityMonths;
        PassMark = passMark;
    }

    public void Activate()
    {
        if (Status != CourseStatus.Draft)
        {
            throw new InvalidStateTransitionException("CRS-011", "Only a draft course can be activated.");
        }

        Status = CourseStatus.Active;
    }

    public void Retire()
    {
        if (Status != CourseStatus.Active)
        {
            throw new InvalidStateTransitionException("CRS-012", "Only an active course can be retired.");
        }

        Status = CourseStatus.Retired;
    }

    private static void Validate(string title, decimal durationHours, int? validityMonths, int passMark)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("CRS-001", "A course title is required.");
        }

        if (durationHours <= 0)
        {
            throw new DomainException("CRS-002", "Course duration must be greater than zero.");
        }

        if (validityMonths is <= 0)
        {
            throw new DomainException("CRS-003", "Validity, when set, must be a positive number of months.");
        }

        if (passMark is < 0 or > 100)
        {
            throw new DomainException("CRS-004", "Pass mark must be between 0 and 100.");
        }
    }
}
