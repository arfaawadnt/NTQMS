using FluentAssertions;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Accreditation;

/// <summary>
/// Audit finding M-15: an evidence link is regulatory proof, so a typed source
/// must reference a real record — a dangling or empty id silently inflates the
/// gap analysis, which counts every link as evidence. 'Other' documents
/// external evidence (paper certificates, third-party reports) and carries no
/// in-system id by definition.
/// </summary>
public class EvidenceLinkTests
{
    private static readonly Guid Set = Guid.CreateVersion7();
    private static readonly Guid Element = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_typed_source_requires_a_record_id()
    {
        var act = () => EvidenceLink.Create(
            Set, Element, EvidenceSourceType.Document, Guid.Empty, "SOP-CAL-1 v2.0", null, Actor, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EVD-003");
    }

    [Fact]
    public void External_evidence_stores_no_record_id()
    {
        var link = EvidenceLink.Create(
            Set, Element, EvidenceSourceType.Other, Guid.CreateVersion7(), "ISO 15189 certificate 2026", null, Actor, Now);

        link.SourceId.Should().Be(Guid.Empty,
            "'Other' evidence lives outside the system; a stored id would be a dangling reference");
    }

    [Fact]
    public void A_typed_source_with_a_record_id_links()
    {
        var sourceId = Guid.CreateVersion7();

        var link = EvidenceLink.Create(
            Set, Element, EvidenceSourceType.Nonconformance, sourceId, "NC-2026-0001", " closure pack ", Actor, Now);

        link.SourceId.Should().Be(sourceId);
        link.SourceRef.Should().Be("NC-2026-0001");
        link.Description.Should().Be("closure pack");
    }

    [Fact]
    public void A_blank_source_reference_is_rejected()
    {
        var act = () => EvidenceLink.Create(
            Set, Element, EvidenceSourceType.Other, Guid.Empty, "  ", null, Actor, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EVD-002");
    }
}
