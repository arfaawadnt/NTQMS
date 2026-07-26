using FluentAssertions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Improvement;

/// <summary>
/// Controlled quality policy (F-11 / ISO 9001 §5.2, ISO 17025 §8.2): versioned,
/// approved by someone other than its author, immutable once active.
/// </summary>
public sealed class QualityPolicyTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Effective = new(2026, 8, 1);

    private static QualityPolicy DraftV1() =>
        QualityPolicy.Draft("QP-2026-0001", 1, "We are committed to accurate, impartial testing.");

    [Fact]
    public void A_draft_starts_unversioned_active_and_editable()
    {
        var p = DraftV1();
        p.Status.Should().Be(QualityPolicyStatus.Draft);
        p.Version.Should().Be(1);

        p.ReviseDraft("Revised commitment.");
        p.Statement.Should().Be("Revised commitment.");
    }

    [Fact]
    public void An_empty_statement_is_rejected()
    {
        var act = () => QualityPolicy.Draft("QP-1", 1, "   ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QP-001");
    }

    [Fact]
    public void Approval_activates_the_policy_and_records_the_approver()
    {
        var p = DraftV1();
        ((IAuditable)p).CreatedByUserId = Guid.CreateVersion7(); // author

        var approver = Guid.CreateVersion7();
        p.Approve(approver, At, Effective);

        p.Status.Should().Be(QualityPolicyStatus.Active);
        p.ApprovedById.Should().Be(approver);
        p.EffectiveDate.Should().Be(Effective);
        p.DomainEvents.OfType<QualityPolicyApproved>().Should().ContainSingle();
    }

    [Fact]
    public void The_author_cannot_approve_their_own_policy()
    {
        var author = Guid.CreateVersion7();
        var p = DraftV1();
        ((IAuditable)p).CreatedByUserId = author;

        var act = () => p.Approve(author, At, Effective);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-QP-001");
        p.Status.Should().Be(QualityPolicyStatus.Draft);
    }

    [Fact]
    public void An_active_policy_cannot_be_edited_or_re_approved()
    {
        var p = DraftV1();
        ((IAuditable)p).CreatedByUserId = Guid.CreateVersion7();
        p.Approve(Guid.CreateVersion7(), At, Effective);

        p.Invoking(x => x.ReviseDraft("no")).Should().Throw<DomainException>().Which.Code.Should().Be("QP-012");
        p.Invoking(x => x.Approve(Guid.CreateVersion7(), At, Effective))
            .Should().Throw<DomainException>().Which.Code.Should().Be("QP-010");
    }

    [Fact]
    public void Only_an_active_policy_can_be_superseded()
    {
        var p = DraftV1();
        p.Invoking(x => x.Supersede()).Should().Throw<DomainException>().Which.Code.Should().Be("QP-011");

        ((IAuditable)p).CreatedByUserId = Guid.CreateVersion7();
        p.Approve(Guid.CreateVersion7(), At, Effective);
        p.Supersede();
        p.Status.Should().Be(QualityPolicyStatus.Superseded);
    }
}
