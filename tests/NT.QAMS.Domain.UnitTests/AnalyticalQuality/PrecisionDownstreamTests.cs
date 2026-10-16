using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

/// <summary>
/// A signed-off precision study's WITHIN-LAB CV feeds measurement uncertainty
/// (u(Rw), ISO/TS 20914) and the sigma metric — never the repeatability CV,
/// and never from an unsigned study.
/// </summary>
public sealed class PrecisionDownstreamTests
{
    private static readonly Guid Signer = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Runs A {10,12}, B {14,16}: within-lab SD 3, mean 13 → CV 23.0769 %.</summary>
    private static PrecisionStudy Study(string analyte = "Glucose", bool signOff = true)
    {
        var study = PrecisionStudy.Configure("PR-2026-0009", analyte, "mmol/L", "Level 2", null, null);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);
        study.Calculate();
        if (signOff)
        {
            study.SignOff(Signer, Now);
        }

        return study;
    }

    private static UncertaintyBudget Budget() => UncertaintyBudget.Create(
        "MU-2026-0004", "glucose", "Hexokinase", "mmol/L", "Level 2", coverageFactor: 2m, targetExpandedUncertainty: null);

    [Fact]
    public void Budget_imports_the_within_lab_cv_as_a_traceable_type_A_component()
    {
        var study = Study();
        var budget = Budget();

        budget.AddPrecisionComponent(study);

        var component = budget.Components.Should().ContainSingle().Subject;
        component.RelativeStandardUncertainty.Should().Be(study.WithinLabCvPct!.Value);
        component.RelativeStandardUncertainty.Should().NotBe(study.RepeatabilityCvPct!.Value);
        component.Type.Should().Be(UncertaintyComponentType.TypeA);
        component.SourcePrecisionStudyId.Should().Be(study.Id);
        component.Source.Should().Contain("PR-2026-0009");

        budget.Calculate();
        budget.ExpandedUncertainty.Should().BeApproximately(2m * 23.0769m, 0.001m);
    }

    [Fact]
    public void Budget_refuses_an_unsigned_foreign_or_duplicate_study()
    {
        Budget().Invoking(b => b.AddPrecisionComponent(Study(signOff: false)))
            .Should().Throw<DomainException>().Which.Code.Should().Be("MU-008");
        Budget().Invoking(b => b.AddPrecisionComponent(Study(analyte: "Urea")))
            .Should().Throw<DomainException>().Which.Code.Should().Be("MU-009");

        var budget = Budget();
        var study = Study();
        budget.AddPrecisionComponent(study);
        budget.Invoking(b => b.AddPrecisionComponent(study))
            .Should().Throw<DomainException>().Which.Code.Should().Be("MU-012");
    }

    [Fact]
    public void Sigma_takes_the_within_lab_cv_and_keeps_tea_and_bias()
    {
        var study = Study();
        var sigma = SigmaAssessment.Create("SIG-2026-0002", "GLUCOSE", "mmol/L", 100m, 7.6923m, 1m);

        sigma.UsePrecisionStudy(study);

        sigma.CvPct.Should().Be(23.0769m);
        sigma.AllowableTotalErrorPct.Should().Be(100m);
        sigma.BiasPct.Should().Be(7.6923m);
        sigma.SigmaValue.Should().Be(4m); // (100 − 7.6923) / 23.0769
        sigma.SourcePrecisionStudyId.Should().Be(study.Id);
        sigma.SourcePrecisionStudyRef.Should().Be("PR-2026-0009");
    }

    [Fact]
    public void A_hand_entered_cv_breaks_the_link_but_editing_tea_alone_keeps_it()
    {
        var study = Study();
        var sigma = SigmaAssessment.Create("SIG-2026-0003", "Glucose", "mmol/L", 50m, 1m, 1m);
        sigma.UsePrecisionStudy(study);

        sigma.SetInputs(60m, 1m, sigma.CvPct);
        sigma.SourcePrecisionStudyId.Should().Be(study.Id);

        sigma.SetInputs(60m, 1m, 2m);
        sigma.SourcePrecisionStudyId.Should().BeNull();
        sigma.SourcePrecisionStudyRef.Should().BeNull();
    }

    [Fact]
    public void Sigma_refuses_an_unsigned_or_foreign_study()
    {
        var sigma = SigmaAssessment.Create("SIG-2026-0004", "Glucose", "mmol/L", 50m, 1m, 1m);
        sigma.Invoking(s => s.UsePrecisionStudy(Study(signOff: false)))
            .Should().Throw<DomainException>().Which.Code.Should().Be("SIG-006");
        sigma.Invoking(s => s.UsePrecisionStudy(Study(analyte: "Urea")))
            .Should().Throw<DomainException>().Which.Code.Should().Be("SIG-007");
    }
}
