using FluentAssertions;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.DocumentControl;

public class ControlledDocumentTests
{
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid DeptHead = Guid.CreateVersion7();
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly Guid FileA = Guid.CreateVersion7();
    private static readonly Guid FileB = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static ControlledDocument NewDoc() =>
        ControlledDocument.Create("sop-cal-045", "Balance calibration", "SOP", FileA, "Initial", Author);

    private static ControlledDocument PublishedDoc()
    {
        var doc = NewDoc();
        doc.SubmitForReview();
        doc.Recommend(DeptHead, Now);
        doc.Publish(Qm, Now);
        return doc;
    }

    [Fact]
    public void Create_normalizes_code_and_starts_at_v1_draft()
    {
        var doc = NewDoc();

        doc.Code.Should().Be("SOP-CAL-045");
        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.InFlightVersion!.VersionLabel.Should().Be("1.0");
        doc.PublishedVersion.Should().BeNull();
    }

    [Fact]
    public void Full_lifecycle_publishes_v1_with_reviewer_and_approver_recorded()
    {
        var doc = PublishedDoc();

        doc.Status.Should().Be(DocumentStatus.Published);
        var v1 = doc.PublishedVersion!;
        v1.RecommendedBy.Should().Be(DeptHead);
        v1.ApprovedBy.Should().Be(Qm);
        doc.DomainEvents.OfType<DocumentPublished>().Should().ContainSingle();
    }

    [Fact]
    public void Author_cannot_recommend_or_approve_own_document()
    {
        var doc = NewDoc();
        doc.SubmitForReview();

        var review = () => doc.Recommend(Author, Now);
        review.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-DOC-001");

        doc.Recommend(DeptHead, Now);

        var approve = () => doc.Publish(Author, Now);
        approve.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-DOC-002");
    }

    [Fact]
    public void Publishing_a_revision_obsoletes_the_previous_version_atomically()
    {
        var doc = PublishedDoc();
        doc.ClearDomainEvents();

        doc.DraftNewVersion(FileB, "Section 4 updated", VersionBump.Minor, Author);
        doc.InFlightVersion!.VersionLabel.Should().Be("1.1");

        doc.SubmitForReview();
        doc.Recommend(DeptHead, Now);
        doc.Publish(Qm, Now);

        doc.PublishedVersion!.VersionLabel.Should().Be("1.1");
        doc.Versions.Single(v => v.VersionLabel == "1.0").State.Should().Be(VersionState.Obsolete);
        doc.DomainEvents.OfType<DocumentVersionObsoleted>().Should().ContainSingle()
            .Which.Version.Should().Be("1.0");
    }

    [Fact]
    public void Major_bump_resets_minor()
    {
        var doc = PublishedDoc();
        doc.DraftNewVersion(FileB, "Full re-issue", VersionBump.Major, Author);
        doc.InFlightVersion!.VersionLabel.Should().Be("2.0");
    }

    [Fact]
    public void Only_one_version_in_flight_at_a_time()
    {
        var doc = PublishedDoc();
        doc.DraftNewVersion(FileB, "Rev A", VersionBump.Minor, Author);

        var second = () => doc.DraftNewVersion(FileB, "Rev B", VersionBump.Minor, Author);
        second.Should().Throw<DomainException>().Which.Code.Should().Be("DOC-016");
    }

    [Fact]
    public void Rejection_returns_version_to_draft_with_reason()
    {
        var doc = NewDoc();
        doc.SubmitForReview();

        doc.RejectVersion(DeptHead, "Missing acceptance criteria");

        doc.InFlightVersion!.State.Should().Be(VersionState.Draft);
        doc.InFlightVersion.RejectionReason.Should().Be("Missing acceptance criteria");
    }

    [Fact]
    public void Retire_obsoletes_published_version_and_blocks_new_versions()
    {
        var doc = PublishedDoc();

        doc.Retire(Qm);

        doc.Status.Should().Be(DocumentStatus.Obsolete);
        doc.PublishedVersion.Should().BeNull();

        var revise = () => doc.DraftNewVersion(FileB, "x", VersionBump.Minor, Author);
        revise.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("DOC-015");
    }
}
