using FluentAssertions;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.TrainingManagement;

public class TrainingCourseTests
{
    private static TrainingCourse Draft() =>
        TrainingCourse.Define("CRS-1", "Hand Hygiene", TrainingCategory.Mandatory, "WHO 5 moments", 1.5m, 12, 80);

    [Fact]
    public void A_defined_course_starts_in_draft()
    {
        var c = Draft();
        c.Status.Should().Be(CourseStatus.Draft);
        c.ValidityMonths.Should().Be(12);
        c.PassMark.Should().Be(80);
    }

    [Theory]
    [InlineData("", 1, 80, "CRS-001")]
    [InlineData("Title", 0, 80, "CRS-002")]
    [InlineData("Title", 1, 101, "CRS-004")]
    public void Define_guards_its_inputs(string title, decimal hours, int passMark, string code)
    {
        var act = () => TrainingCourse.Define("CRS-X", title, TrainingCategory.Clinical, "d", hours, 12, passMark);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Validity_when_present_must_be_positive()
    {
        var act = () => TrainingCourse.Define("CRS-X", "T", TrainingCategory.Cme, "d", 1m, 0, 80);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CRS-003");
    }

    [Fact]
    public void Only_a_draft_can_be_edited_or_activated()
    {
        var c = Draft();
        c.Activate();
        c.Status.Should().Be(CourseStatus.Active);

        var edit = () => c.UpdateDetails("New", TrainingCategory.Safety, "d", 2m, null, 70);
        edit.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CRS-010");

        var reactivate = () => c.Activate();
        reactivate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CRS-011");
    }

    [Fact]
    public void Only_an_active_course_can_be_retired()
    {
        var c = Draft();
        var early = c.Retire;
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CRS-012");

        c.Activate();
        c.Retire();
        c.Status.Should().Be(CourseStatus.Retired);
    }
}
