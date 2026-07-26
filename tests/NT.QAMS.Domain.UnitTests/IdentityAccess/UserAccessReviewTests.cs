using FluentAssertions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.IdentityAccess;

/// <summary>
/// Periodic user-access review (F-11 / 21 CFR Part 11 §11.10(d), EU Annex 11 §12):
/// a recurring recertification that accounts and roles are still appropriate.
/// </summary>
public sealed class UserAccessReviewTests
{
    private static readonly DateOnly Today = new(2026, 7, 27);
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Completing_snapshots_coverage_and_raises_the_event()
    {
        var review = UserAccessReview.Open("UAR-2026-0001", Today);
        review.Status.Should().Be(UserAccessReviewStatus.Open);

        var reviewer = Guid.CreateVersion7();
        review.Complete(reviewer, Now, accountsReviewed: 12, changesRequired: true,
            "Two dormant analyst accounts deactivated; all other roles confirmed.");

        review.Status.Should().Be(UserAccessReviewStatus.Completed);
        review.AccountsReviewed.Should().Be(12);
        review.ChangesRequired.Should().BeTrue();
        review.ReviewedBy.Should().Be(reviewer);
        review.DomainEvents.OfType<UserAccessReviewCompleted>().Should().ContainSingle();
    }

    [Fact]
    public void A_conclusion_is_required()
    {
        var review = UserAccessReview.Open("UAR-1", Today);
        var act = () => review.Complete(Guid.CreateVersion7(), Now, 5, false, "  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("UAR-011");
    }

    [Fact]
    public void A_completed_review_is_immutable()
    {
        var review = UserAccessReview.Open("UAR-1", Today);
        review.Complete(Guid.CreateVersion7(), Now, 5, false, "All access appropriate.");

        var again = () => review.Complete(Guid.CreateVersion7(), Now, 5, false, "again");
        again.Should().Throw<DomainException>().Which.Code.Should().Be("UAR-010");
    }
}
