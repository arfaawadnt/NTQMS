using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class SigmaAssessmentTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sigma_is_tea_minus_bias_over_cv_with_the_matching_grade()
    {
        // TEa 10%, bias 2%, CV 1% → σ = (10−2)/1 = 8 → world class.
        var a = SigmaAssessment.Create("SIG-2026-0001", "HbA1c", "%", 10m, 2m, 1m);
        a.SigmaValue.Should().Be(8m);
        a.Grade.Should().Be(SigmaGrade.WorldClass);
        a.QcRecommendation.Should().Contain("N=2");

        // TEa 10%, bias 2%, CV 2% → σ = 4 → good.
        a.SetInputs(10m, 2m, 2m);
        a.SigmaValue.Should().Be(4m);
        a.Grade.Should().Be(SigmaGrade.Good);

        // |bias| is used regardless of sign.
        a.SetInputs(10m, -2m, 2m);
        a.SigmaValue.Should().Be(4m);
    }

    [Fact]
    public void Bias_exceeding_tea_floors_sigma_at_zero_unacceptable()
    {
        var a = SigmaAssessment.Create("SIG-1", "X", "u", 5m, 6m, 2m);
        a.SigmaValue.Should().Be(0m);
        a.Grade.Should().Be(SigmaGrade.Unacceptable);
        a.QcRecommendation.Should().Contain("replace");
    }

    [Fact]
    public void Grade_bands_are_inclusive_lower_bounds()
    {
        var a = SigmaAssessment.Create("SIG-1", "X", "u", 6m, 0m, 2m); // σ = 3 exactly
        a.Grade.Should().Be(SigmaGrade.Marginal);
        a.SetInputs(10m, 0m, 2m); // σ = 5 exactly
        a.Grade.Should().Be(SigmaGrade.Excellent);
    }

    [Fact]
    public void Inputs_are_guarded_and_sign_off_freezes()
    {
        var zeroCv = () => SigmaAssessment.Create("SIG-1", "X", "u", 10m, 2m, 0m);
        zeroCv.Should().Throw<DomainException>().Which.Code.Should().Be("SIG-003");

        var zeroTea = () => SigmaAssessment.Create("SIG-1", "X", "u", 0m, 2m, 1m);
        zeroTea.Should().Throw<DomainException>().Which.Code.Should().Be("SIG-002");

        var a = SigmaAssessment.Create("SIG-1", "Glucose", "mg/dL", 10m, 2m, 2m);
        a.SignOff(Qm, Now);
        a.State.Should().Be(SigmaAssessmentState.SignedOff);
        a.DomainEvents.Should().ContainSingle(e => e is SigmaAssessmentSignedOff);

        var mutate = () => a.SetInputs(12m, 1m, 1m);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SIG-010");
    }
}
