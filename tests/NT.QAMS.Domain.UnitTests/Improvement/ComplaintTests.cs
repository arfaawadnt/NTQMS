using FluentAssertions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Improvement;

public class ComplaintTests
{
    private static readonly Guid Logger = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static Complaint Logged() => Complaint.Log(
        "CMP-2026-0001", ComplaintChannel.Email, "Dr. Client", "client@lab.example",
        confidential: false, "Late report", "The report for order 123 arrived two weeks late.",
        Logger, Now);

    private static Complaint Validated()
    {
        var complaint = Logged();
        complaint.Acknowledge(Now.AddHours(1));
        complaint.RecordValidationVerdict(justified: true, "Turnaround breach confirmed against the SLA.");
        return complaint;
    }

    [Fact]
    public void Log_starts_in_logged_and_raises_event()
    {
        var complaint = Logged();

        complaint.Status.Should().Be(ComplaintStatus.Logged);
        complaint.DomainEvents.Should().ContainSingle(e => e is ComplaintLogged);
    }

    [Fact]
    public void Log_requires_complainant_and_subject()
    {
        var act1 = () => Complaint.Log("CMP-1", ComplaintChannel.Phone, " ", null, false, "S", "D", Logger, Now);
        var act2 = () => Complaint.Log("CMP-1", ComplaintChannel.Phone, "N", null, false, " ", "D", Logger, Now);

        act1.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-001");
        act2.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-002");
    }

    [Fact]
    public void Justified_validation_moves_to_validated_and_demands_an_nc()
    {
        var complaint = Validated();

        complaint.Status.Should().Be(ComplaintStatus.Validated);
        complaint.DomainEvents.Should().ContainSingle(e => e is ComplaintValidated);
    }

    [Fact]
    public void Unjustified_validation_terminates_as_invalid_without_nc()
    {
        var complaint = Logged();
        complaint.Acknowledge(Now.AddHours(1));

        complaint.RecordValidationVerdict(justified: false, "Report was delivered on time per the portal log.");

        complaint.Status.Should().Be(ComplaintStatus.Invalid);
        complaint.DomainEvents.Should().NotContain(e => e is ComplaintValidated);
    }

    [Fact]
    public void Validation_requires_a_reason()
    {
        var complaint = Logged();
        complaint.Acknowledge(Now.AddHours(1));

        var act = () => complaint.RecordValidationVerdict(true, " ");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-003");
    }

    [Fact]
    public void Full_happy_path_reaches_closed()
    {
        var complaint = Validated();
        complaint.StartInvestigation();
        complaint.LogOutcome("Courier handover step missed; process amended.");
        complaint.Resolve("Client credited; corrective action tracked under the linked NC.");

        complaint.Close(linkedNcClosed: true);

        complaint.Status.Should().Be(ComplaintStatus.Closed);
    }

    [Fact]
    public void Close_is_blocked_while_the_linked_nc_is_open()
    {
        var complaint = Validated();
        complaint.LinkNc(Guid.CreateVersion7());
        complaint.StartInvestigation();
        complaint.LogOutcome("O");
        complaint.Resolve("R");

        var act = () => complaint.Close(linkedNcClosed: false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-020");
    }

    [Fact]
    public void Transitions_out_of_order_are_rejected()
    {
        var complaint = Logged();

        var act = () => complaint.StartInvestigation();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void LinkNc_is_idempotent_and_keeps_the_first_link()
    {
        var complaint = Validated();
        var first = Guid.CreateVersion7();

        complaint.LinkNc(first);
        complaint.LinkNc(Guid.CreateVersion7());

        complaint.LinkedNcId.Should().Be(first);
    }
}
