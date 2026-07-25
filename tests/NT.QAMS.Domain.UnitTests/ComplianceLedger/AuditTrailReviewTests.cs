using FluentAssertions;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.ComplianceLedger;

public sealed class AuditTrailReviewTests
{
    private static readonly Guid Reviewer = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Period_must_be_coherent()
    {
        var inverted = () => AuditTrailReview.Open("ATR-1", new DateOnly(2026, 7, 1), new DateOnly(2026, 6, 1));
        inverted.Should().Throw<DomainException>().Which.Code.Should().Be("ATR-001");
    }

    [Fact]
    public void Completion_snapshots_coverage_and_is_immutable()
    {
        var review = AuditTrailReview.Open("ATR-1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        var blank = () => review.Complete(Reviewer, Now, 100, 40, false, " ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be("ATR-011");

        review.Complete(Reviewer, Now, 1234, 567, anomaliesFound: false,
            "All entries consistent; no gaps in sequence; no after-hours mutations.");
        review.EventsReviewed.Should().Be(1234);
        review.FieldChangesReviewed.Should().Be(567);
        review.DomainEvents.Should().BeEmpty("a clean review is not an incident");

        var again = () => review.Complete(Reviewer, Now, 1, 1, false, "again");
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("ATR-010");
    }

    [Fact]
    public void Anomalies_raise_the_event_that_opens_an_nc()
    {
        var review = AuditTrailReview.Open("ATR-2", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        review.Complete(Reviewer, Now, 900, 300, anomaliesFound: true,
            "Cluster of credential-adjacent field changes at 03:00 on 2026-06-14 without a matching change request.");

        var anomaly = review.DomainEvents.OfType<AuditTrailAnomalyFound>().Single();
        anomaly.ReviewedBy.Should().Be(Reviewer);
        anomaly.Conclusion.Should().Contain("03:00");
    }
}
