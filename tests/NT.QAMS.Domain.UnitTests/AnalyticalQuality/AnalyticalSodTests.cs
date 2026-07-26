using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

/// <summary>
/// Segregation of duties on analytical sign-off (F-05 / Part 11 §11.10(g)):
/// the person who prepared a study cannot also sign it off. The preparer id is
/// stamped by the audit interceptor in production; here it is set directly.
/// </summary>
public sealed class AnalyticalSodTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static OutlierScreening CalculatedScreening()
    {
        var s = OutlierScreening.Configure("OUT-1", "batch", "u");
        foreach (var v in new[] { 10m, 11m, 12m, 13m, 100m }) { s.AddPoint(v, "p"); }
        s.Calculate();
        return s;
    }

    [Fact]
    public void Preparer_cannot_sign_off_their_own_study()
    {
        var preparer = Guid.CreateVersion7();
        var s = CalculatedScreening();
        ((IAuditable)s).CreatedByUserId = preparer;

        var act = () => s.SignOff(preparer, At);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-AQ-001");
        s.State.Should().Be(OutlierScreeningState.Calculated, "the illegal sign-off must not change state");
    }

    [Fact]
    public void A_different_authorized_user_may_sign_off()
    {
        var s = CalculatedScreening();
        ((IAuditable)s).CreatedByUserId = Guid.CreateVersion7();

        s.SignOff(Guid.CreateVersion7(), At);

        s.State.Should().Be(OutlierScreeningState.SignedOff);
    }

    [Fact]
    public void Sign_off_is_allowed_when_the_preparer_is_unknown()
    {
        // Records created before the id was captured (or by system work) have no
        // preparer id — the guard is a no-op rather than blocking all sign-off.
        var s = CalculatedScreening();
        s.SignOff(Guid.CreateVersion7(), At);
        s.State.Should().Be(OutlierScreeningState.SignedOff);
    }
}
