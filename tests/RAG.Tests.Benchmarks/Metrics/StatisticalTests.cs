namespace RAG.Tests.Benchmarks.Metrics;

/// <summary>
/// Statistical tests for comparing retrieval strategy performance.
/// </summary>
public static class StatisticalTests
{
    /// <summary>
    /// Performs a paired t-test to compare two samples.
    /// H0: mean(sample1) = mean(sample2)
    /// H1: mean(sample1) ≠ mean(sample2) (two-tailed test)
    /// </summary>
    /// <param name="sample1">First sample (e.g., BM25 precision scores)</param>
    /// <param name="sample2">Second sample (e.g., Hybrid precision scores)</param>
    /// <returns>Tuple of (t-statistic, p-value, significant at 0.05 level)</returns>
    public static (double TStatistic, double PValue, bool IsSignificant) PairedTTest(
        double[] sample1,
        double[] sample2)
    {
        if (sample1.Length != sample2.Length)
            throw new ArgumentException("Samples must have equal length for paired t-test");

        if (sample1.Length < 2)
            throw new ArgumentException("At least 2 paired observations required");

        // Calculate paired differences
        var differences = sample1.Zip(sample2, (a, b) => a - b).ToArray();

        // Calculate mean difference
        var meanDiff = differences.Average();

        // Calculate standard deviation of differences
        var variance = differences.Sum(d => Math.Pow(d - meanDiff, 2)) / (differences.Length - 1);
        var stdDev = Math.Sqrt(variance);

        // Calculate t-statistic
        var standardError = stdDev / Math.Sqrt(differences.Length);
        var tStatistic = meanDiff / standardError;

        // Degrees of freedom
        var degreesOfFreedom = differences.Length - 1;

        // Calculate two-tailed p-value using Student's t-distribution
        var pValue = 2 * (1 - StudentTCDF(Math.Abs(tStatistic), degreesOfFreedom));

        // Significance at alpha = 0.05
        var isSignificant = pValue < 0.05;

        return (tStatistic, pValue, isSignificant);
    }

    /// <summary>
    /// Calculates the cumulative distribution function for Student's t-distribution.
    /// Uses approximation for practical purposes.
    /// </summary>
    private static double StudentTCDF(double t, int df)
    {
        // For large df (> 30), t-distribution approximates normal distribution
        if (df > 30)
        {
            return NormalCDF(t);
        }

        // Use numerical approximation for smaller df
        // This is a simplified implementation; for production, consider using MathNet.Numerics
        var x = df / (df + t * t);
        var beta = IncompleteBeta(x, df / 2.0, 0.5);
        return 1 - 0.5 * beta;
    }

    /// <summary>
    /// Approximation of the standard normal CDF using the error function.
    /// </summary>
    private static double NormalCDF(double x)
    {
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    /// <summary>
    /// Error function approximation using Abramowitz and Stegun formula.
    /// </summary>
    private static double Erf(double x)
    {
        // Constants
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        // Save the sign of x
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        // Abramowitz and Stegun formula
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    /// <summary>
    /// Incomplete beta function approximation.
    /// Simplified implementation for t-test CDF calculation.
    /// </summary>
    private static double IncompleteBeta(double x, double a, double b)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        // Use continued fraction approximation
        // This is a simplified version; for production, use a proper numerical library
        var bt = Math.Exp(a * Math.Log(x) + b * Math.Log(1 - x));

        if (x < (a + 1) / (a + b + 2))
        {
            return bt * BetaContinuedFraction(x, a, b) / a;
        }
        else
        {
            return 1 - bt * BetaContinuedFraction(1 - x, b, a) / b;
        }
    }

    /// <summary>
    /// Continued fraction for incomplete beta function.
    /// </summary>
    private static double BetaContinuedFraction(double x, double a, double b)
    {
        const int maxIterations = 100;
        const double epsilon = 1e-10;

        double am = 1.0;
        double bm = 1.0;
        double az = 1.0;
        double qab = a + b;
        double qap = a + 1.0;
        double qam = a - 1.0;
        double bz = 1.0 - qab * x / qap;

        for (int m = 1; m <= maxIterations; m++)
        {
            double em = m;
            double tem = em + em;
            double d = em * (b - m) * x / ((qam + tem) * (a + tem));
            double ap = az + d * am;
            double bp = bz + d * bm;
            d = -(a + em) * (qab + em) * x / ((a + tem) * (qap + tem));
            double app = ap + d * az;
            double bpp = bp + d * bz;
            double aold = az;
            am = ap / bpp;
            bm = bp / bpp;
            az = app / bpp;
            bz = 1.0;
            if (Math.Abs(az - aold) < epsilon * Math.Abs(az))
                return az;
        }

        return az;
    }

    /// <summary>
    /// Gets significance indicator string for p-value.
    /// * p &lt; 0.05
    /// ** p &lt; 0.01
    /// *** p &lt; 0.001
    /// </summary>
    public static string GetSignificanceIndicator(double pValue)
    {
        if (pValue < 0.001) return "***";
        if (pValue < 0.01) return "**";
        if (pValue < 0.05) return "*";
        return "";
    }

    /// <summary>
    /// Calculates percentage improvement from baseline.
    /// </summary>
    public static double CalculateImprovementPercentage(double baseline, double improved)
    {
        if (baseline == 0) return 0;
        return ((improved - baseline) / baseline) * 100;
    }
}
