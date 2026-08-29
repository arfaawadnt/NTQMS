using FluentAssertions;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Governance;

/// <summary>
/// HQMS M16 completion: the supplier contract / SLA register, the corrective-action-request loop,
/// and the outsourced-clinical-service flag.
/// </summary>
public class SupplierContractsAndCarsTests
{
    private static readonly Guid Registrant = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static Supplier Ref() => Supplier.Register("SUP-1", "Ref Lab Co", "OutsourcedService", Registrant, true, "Reference lab — microbiology");

    [Fact]
    public void An_outsourced_service_supplier_carries_its_scope()
    {
        var s = Ref();
        s.IsOutsourcedClinicalService.Should().BeTrue();
        s.ServiceScope.Should().Be("Reference lab — microbiology");
    }

    [Fact]
    public void A_contract_is_active_then_terminated_and_expiry_is_derived()
    {
        var s = Ref();
        var id = s.AddContract("SCT-1", "Microbiology referral SLA", Today, Today.AddYears(1), "TAT 48h; crit-value callback 1h");
        var contract = s.Contracts.Single();
        contract.Status.Should().Be(ContractStatus.Active);
        contract.IsExpired(Today.AddMonths(6)).Should().BeFalse();
        contract.IsExpired(Today.AddYears(2)).Should().BeTrue("past its end date");

        s.TerminateContract(id, "Provider changed");
        contract.Status.Should().Be(ContractStatus.Terminated);
        contract.IsExpired(Today.AddYears(2)).Should().BeFalse("a terminated contract is not counted as expired");
    }

    [Fact]
    public void A_contract_end_cannot_precede_its_start()
    {
        var s = Ref();
        var act = () => s.AddContract("SCT-2", "Bad", Today, Today.AddDays(-1), null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SUP-031");
    }

    [Fact]
    public void A_car_runs_open_then_response_then_closed()
    {
        var s = Ref();
        var id = s.RaiseCar("Delayed critical result", Today, Today.AddDays(14));
        var car = s.Cars.Single();
        car.Status.Should().Be(SupplierCarStatus.Open);
        s.OpenCarCount.Should().Be(1);

        var earlyClose = () => s.CloseCar(id, true, "x");
        earlyClose.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SUP-044");

        s.RecordCarResponse(id, "Root cause: courier delay; new SLA agreed.", Today.AddDays(5));
        car.Status.Should().Be(SupplierCarStatus.ResponseReceived);

        s.CloseCar(id, effective: true, "Verified over next quarter — no recurrence.");
        car.Status.Should().Be(SupplierCarStatus.Closed);
        car.Effective.Should().BeTrue();
        s.OpenCarCount.Should().Be(0);
    }

    [Fact]
    public void An_open_car_past_its_due_date_is_overdue()
    {
        var s = Ref();
        s.RaiseCar("x", Today, Today.AddDays(7));
        var car = s.Cars.Single();
        car.IsOverdue(Today.AddDays(7)).Should().BeFalse();
        car.IsOverdue(Today.AddDays(8)).Should().BeTrue();
    }

    [Fact]
    public void A_car_cannot_be_closed_without_a_response()
    {
        var s = Ref();
        var id = s.RaiseCar("x", Today, null);
        var act = () => s.CloseCar(id, true, "note");
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SUP-044");
    }

    [Fact]
    public void A_car_response_cannot_predate_the_car_being_raised()
    {
        // N-13: temporal-order guard.
        var s = Ref();
        var id = s.RaiseCar("Delayed result", Today, Today.AddDays(14));
        var act = () => s.RecordCarResponse(id, "backdated", Today.AddDays(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SUP-CAR-010");
    }
}
