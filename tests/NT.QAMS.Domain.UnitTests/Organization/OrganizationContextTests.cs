using FluentAssertions;
using NT.QAMS.Domain.Organization;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Organization;

public sealed class OrganizationContextTests
{
    [Fact]
    public void Interested_party_is_a_living_entry_until_archived()
    {
        var party = InterestedParty.Register(
            "IP-2026-0001", "EGAC (accreditation body)", "Accreditation body",
            "Continued conformity with ISO 15189; timely CAPA responses.",
            "Maintain accreditation scope; respond to findings within 30 days.",
            new DateOnly(2026, 7, 1));

        party.Revise("EGAC", "Accreditation body",
            "Continued conformity; timely CAPA responses; annual surveillance readiness.",
            null, new DateOnly(2026, 7, 25));
        party.NeedsAndExpectations.Should().Contain("surveillance");

        party.Archive();
        var lateRevise = () => party.Revise("X", "Y", "Z", null, new DateOnly(2026, 8, 1));
        lateRevise.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("IP-010");
    }

    [Fact]
    public void Interested_party_demands_the_needs_that_justify_it()
    {
        var noNeeds = () => InterestedParty.Register(
            "IP-1", "Someone", "Customer", " ", null, new DateOnly(2026, 7, 1));
        noNeeds.Should().Throw<DomainException>().Which.Code.Should().Be("IP-002");
    }

    [Fact]
    public void Context_issue_closes_with_a_resolution_and_freezes()
    {
        var issue = ContextIssue.Register(
            "CTX-2026-0001", ContextIssueType.External, "Threat",
            "Single-source reagent supply for chemistry line", "Stock-out would halt testing within 2 weeks.");

        issue.LinkRisk(Guid.CreateVersion7());
        issue.LinkedRiskId.Should().NotBeNull();

        var blank = () => issue.Close(" ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be("CTX-003");

        issue.Close("Second supplier qualified (SUP-2026-0009); risk retired.");
        issue.Status.Should().Be(ContextIssueStatus.Closed);

        var lateEdit = () => issue.Revise(ContextIssueType.External, "Threat", "x", "y");
        lateEdit.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CTX-010");
    }
}
