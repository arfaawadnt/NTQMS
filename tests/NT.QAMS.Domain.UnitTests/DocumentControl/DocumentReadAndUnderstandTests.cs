using FluentAssertions;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.DocumentControl;

public class DocumentReadAndUnderstandTests
{
    private static ControlledDocument Draft() =>
        ControlledDocument.Create("SOP-CAL-1", "Calibration SOP", "SOP", Guid.CreateVersion7(), "Initial", Guid.CreateVersion7());

    [Fact]
    public void By_department_requires_at_least_one_department()
    {
        var act = () => Draft().SetReadAndUnderstand(true, DocumentAudienceScope.ByDepartment, []);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DOC-031");
    }

    [Fact]
    public void By_department_sets_scope_and_departments()
    {
        var doc = Draft();
        var deptA = Guid.CreateVersion7();
        var deptB = Guid.CreateVersion7();

        doc.SetReadAndUnderstand(true, DocumentAudienceScope.ByDepartment, [deptA, deptB, deptA]);

        doc.RequiresAcknowledgement.Should().BeTrue();
        doc.AudienceScope.Should().Be(DocumentAudienceScope.ByDepartment);
        doc.AudienceDepartments.Select(a => a.DepartmentId).Should().BeEquivalentTo([deptA, deptB]);
    }

    [Fact]
    public void All_staff_required_carries_no_departments()
    {
        var doc = Draft();
        doc.SetReadAndUnderstand(true, DocumentAudienceScope.AllStaff, [Guid.CreateVersion7()]);

        doc.RequiresAcknowledgement.Should().BeTrue();
        doc.AudienceScope.Should().Be(DocumentAudienceScope.AllStaff);
        doc.AudienceDepartments.Should().BeEmpty();
    }

    [Fact]
    public void Not_required_clears_the_audience()
    {
        var doc = Draft();
        doc.SetReadAndUnderstand(true, DocumentAudienceScope.ByDepartment, [Guid.CreateVersion7()]);

        doc.SetReadAndUnderstand(false, DocumentAudienceScope.ByDepartment, [Guid.CreateVersion7()]);

        doc.RequiresAcknowledgement.Should().BeFalse();
        doc.AudienceScope.Should().Be(DocumentAudienceScope.AllStaff);
        doc.AudienceDepartments.Should().BeEmpty();
    }

    [Fact]
    public void A_retired_document_distribution_cannot_change()
    {
        var doc = Draft();
        doc.Retire(Guid.CreateVersion7());

        var act = () => doc.SetReadAndUnderstand(true, DocumentAudienceScope.AllStaff, []);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("DOC-030");
    }
}
