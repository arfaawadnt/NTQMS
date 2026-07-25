using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class ReferenceIntervalStudyTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static ReferenceIntervalStudy New() => ReferenceIntervalStudy.Configure(
        "RI-2026-0001", "Sodium", "mmol/L", "Adult", "Manufacturer insert", 136m, 145m);

    private static void AddInside(ReferenceIntervalStudy s, int count)
    {
        for (var i = 0; i < count; i++)
        {
            s.AddSample(140m, $"S{i}");
        }
    }

    [Fact]
    public void Two_of_twenty_outside_still_verifies_the_interval()
    {
        var study = New();
        AddInside(study, 18);
        study.AddSample(135m, "low");   // below 136
        study.AddSample(146m, "high");  // above 145

        study.Calculate();

        study.OutsideCount.Should().Be(2);
        study.AllowedOutside.Should().Be(2); // floor(20 * 0.10)
        study.Verdict.Should().Be(ReferenceIntervalVerdict.Verified);
    }

    [Fact]
    public void Three_of_twenty_outside_rejects_the_transference()
    {
        var study = New();
        AddInside(study, 17);
        study.AddSample(130m, null);
        study.AddSample(131m, null);
        study.AddSample(150m, null);

        study.Calculate();

        study.OutsideCount.Should().Be(3);
        study.Verdict.Should().Be(ReferenceIntervalVerdict.Rejected);
    }

    [Fact]
    public void Boundary_values_count_as_inside()
    {
        var study = New();
        AddInside(study, 18);
        study.AddSample(136m, "at-lower"); // == lower, inside
        study.AddSample(145m, "at-upper"); // == upper, inside

        study.Calculate();

        study.OutsideCount.Should().Be(0);
        study.Verdict.Should().Be(ReferenceIntervalVerdict.Verified);
    }

    [Fact]
    public void Adding_samples_invalidates_a_prior_calculation()
    {
        var study = New();
        AddInside(study, 20);
        study.Calculate();
        study.Verdict.Should().Be(ReferenceIntervalVerdict.Verified);

        study.AddSample(100m, "outlier");
        study.State.Should().Be(ReferenceIntervalState.DataEntry);
        study.Verdict.Should().BeNull();
    }

    [Fact]
    public void Guards_config_minimum_samples_and_sign_off_freeze()
    {
        var invertedInterval = () => ReferenceIntervalStudy.Configure(
            "RI-1", "X", "u", "Adult", "insert", 10m, 5m);
        invertedInterval.Should().Throw<DomainException>().Which.Code.Should().Be("RI-003");

        var study = New();
        AddInside(study, 19);
        var tooFew = () => study.Calculate();
        tooFew.Should().Throw<DomainException>().Which.Code.Should().Be("RI-010");

        study.AddSample(140m, null);
        study.Calculate();
        study.SignOff(Qm, Now);
        study.DomainEvents.Should().ContainSingle(e => e is ReferenceIntervalSignedOff);

        var mutate = () => study.AddSample(140m, null);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("RI-012");
    }
}
