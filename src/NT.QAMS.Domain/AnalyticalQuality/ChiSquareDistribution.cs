namespace NT.QAMS.Domain.AnalyticalQuality;

/// <summary>
/// The χ² distribution for the imprecision statistics that CLSI EP05-A3 /
/// EP15-A3 prescribe: confidence intervals on an SD and the upper verification
/// limit of a manufacturer's precision claim. Degrees of freedom may be
/// non-integer — a within-laboratory SD carries Satterthwaite effective df.
/// Pure numerics (Lanczos log-gamma + regularised incomplete gamma), so the
/// domain owns its statistics without a third-party package.
/// </summary>
public static class ChiSquareDistribution
{
    private const int MaxIterations = 500;
    private const double Epsilon = 1e-14;
    private const double Tiny = 1e-300;
    private const double QuantileTolerance = 1e-12;

    private static readonly double[] LanczosCoefficients =
    [
        0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
        -176.61502916214059, 12.507343278686905, -0.13857109526572012,
        9.9843695780195716e-6, 1.5056327351493116e-7,
    ];

    /// <summary>P(X ≤ x) for X ~ χ²(df).</summary>
    public static double Cdf(double x, double degreesOfFreedom)
    {
        RequirePositive(degreesOfFreedom);
        return x <= 0 ? 0 : RegularizedLowerGamma(degreesOfFreedom / 2.0, x / 2.0);
    }

    /// <summary>The value x with P(X ≤ x) = p for X ~ χ²(df), 0 &lt; p &lt; 1.</summary>
    public static double Quantile(double probability, double degreesOfFreedom)
    {
        RequirePositive(degreesOfFreedom);
        if (probability is <= 0 or >= 1 || double.IsNaN(probability))
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "The probability must lie strictly between 0 and 1.");
        }

        // The CDF is monotone, so bracketing + bisection is unconditionally
        // convergent for every df (Newton steps can overshoot at small df).
        double low = 0, high = Math.Max(1.0, degreesOfFreedom);
        while (Cdf(high, degreesOfFreedom) < probability)
        {
            low = high;
            high *= 2;
        }

        for (var i = 0; i < MaxIterations && high - low > QuantileTolerance * Math.Max(1.0, high); i++)
        {
            var mid = (low + high) / 2;
            if (Cdf(mid, degreesOfFreedom) < probability)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) / 2;
    }

    private static double RegularizedLowerGamma(double a, double x)
    {
        if (x < a + 1)
        {
            // Series representation.
            double term = 1 / a, sum = term, ap = a;
            for (var n = 0; n < MaxIterations; n++)
            {
                ap += 1;
                term *= x / ap;
                sum += term;
                if (Math.Abs(term) < Math.Abs(sum) * Epsilon)
                {
                    break;
                }
            }

            return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }

        // Continued fraction for Q(a, x) (modified Lentz), P = 1 − Q.
        double b = x + 1 - a, c = 1 / Tiny, d = 1 / b, h = d;
        for (var i = 1; i <= MaxIterations; i++)
        {
            var an = -i * (i - a);
            b += 2;
            d = an * d + b;
            if (Math.Abs(d) < Tiny)
            {
                d = Tiny;
            }

            c = b + an / c;
            if (Math.Abs(c) < Tiny)
            {
                c = Tiny;
            }

            d = 1 / d;
            var delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < Epsilon)
            {
                break;
            }
        }

        return 1 - Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    private static double LogGamma(double z)
    {
        if (z < 0.5)
        {
            // Reflection: Γ(z)Γ(1−z) = π / sin(πz).
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * z))) - LogGamma(1 - z);
        }

        z -= 1;
        var x = LanczosCoefficients[0];
        for (var i = 1; i < LanczosCoefficients.Length; i++)
        {
            x += LanczosCoefficients[i] / (z + i);
        }

        var t = z + LanczosCoefficients.Length - 1.5;
        return 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x);
    }

    private static void RequirePositive(double degreesOfFreedom)
    {
        if (!(degreesOfFreedom > 0) || double.IsInfinity(degreesOfFreedom))
        {
            throw new ArgumentOutOfRangeException(nameof(degreesOfFreedom), "Degrees of freedom must be positive and finite.");
        }
    }
}
