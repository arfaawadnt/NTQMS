using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

/// <summary>
/// The χ² quantiles behind every EP05/EP15 confidence interval and
/// verification limit, pinned against published table values.
/// </summary>
public sealed class ChiSquareDistributionTests
{
    [Theory]
    [InlineData(0.95, 1, 3.8415)]
    [InlineData(0.95, 2, 5.9915)]
    [InlineData(0.975, 2, 7.3778)]
    [InlineData(0.025, 4, 0.4844)]
    [InlineData(0.975, 4, 11.1433)]
    [InlineData(0.025, 10, 3.2470)]
    [InlineData(0.975, 10, 20.4832)]
    [InlineData(0.95, 20, 31.4104)]
    [InlineData(0.975, 20, 34.1696)]
    [InlineData(0.99, 20, 37.5662)]
    [InlineData(0.95, 76, 97.3510)]
    public void Quantile_matches_published_tables(double p, double df, double expected) =>
        ChiSquareDistribution.Quantile(p, df).Should().BeApproximately(expected, 0.0005);

    [Theory]
    [InlineData(0.05, 1.2558)]
    [InlineData(0.5, 3.75)]
    [InlineData(0.975, 17.3)]
    public void Quantile_inverts_the_cdf_at_non_integer_df(double p, double df) =>
        ChiSquareDistribution.Cdf(ChiSquareDistribution.Quantile(p, df), df).Should().BeApproximately(p, 1e-9);

    [Fact]
    public void Cdf_is_zero_at_and_below_zero() =>
        ChiSquareDistribution.Cdf(0, 5).Should().Be(0);

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(0.5, 0)]
    [InlineData(0.5, -1)]
    public void Out_of_range_arguments_are_rejected(double p, double df) =>
        FluentActions.Invoking(() => ChiSquareDistribution.Quantile(p, df))
            .Should().Throw<ArgumentOutOfRangeException>();
}
