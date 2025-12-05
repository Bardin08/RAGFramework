using Microsoft.Extensions.Logging;

namespace RAG.Evaluation.Services;

/// <summary>
/// Provides statistical testing methods for comparing experiment variants.
/// </summary>
public class StatisticalTestService
{
    private readonly ILogger<StatisticalTestService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatisticalTestService"/> class.
    /// </summary>
    public StatisticalTestService(ILogger<StatisticalTestService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs a paired t-test to compare two samples.
    /// </summary>
    /// <param name="sample1">First sample values.</param>
    /// <param name="sample2">Second sample values (must be same length as sample1).</param>
    /// <returns>T-statistic and p-value.</returns>
    public virtual (double tStatistic, double pValue) PairedTTest(double[] sample1, double[] sample2)
    {
        ArgumentNullException.ThrowIfNull(sample1);
        ArgumentNullException.ThrowIfNull(sample2);

        if (sample1.Length != sample2.Length)
        {
            throw new ArgumentException("Samples must have the same length for paired t-test");
        }

        if (sample1.Length < 2)
        {
            _logger.LogWarning("Sample size too small for t-test (n={Size})", sample1.Length);
            return (0.0, 1.0);
        }

        // Calculate paired differences
        var differences = new double[sample1.Length];
        for (int i = 0; i < sample1.Length; i++)
        {
            differences[i] = sample1[i] - sample2[i];
        }

        // Calculate mean of differences
        var meanDiff = differences.Average();

        // Calculate standard deviation of differences
        var variance = differences.Sum(d => Math.Pow(d - meanDiff, 2)) / (differences.Length - 1);
        var stdDev = Math.Sqrt(variance);

        // Calculate t-statistic
        var standardError = stdDev / Math.Sqrt(differences.Length);
        var tStatistic = standardError > 0 ? meanDiff / standardError : 0.0;

        // Calculate p-value using two-tailed test
        var degreesOfFreedom = differences.Length - 1;
        var pValue = CalculateTwoTailedPValue(Math.Abs(tStatistic), degreesOfFreedom);

        _logger.LogDebug(
            "Paired t-test: n={SampleSize}, mean_diff={MeanDiff:F4}, t={TStatistic:F4}, p={PValue:F6}",
            differences.Length,
            meanDiff,
            tStatistic,
            pValue);

        return (tStatistic, pValue);
    }

    /// <summary>
    /// Applies Bonferroni correction for multiple comparisons.
    /// </summary>
    /// <param name="pValue">Original p-value.</param>
    /// <param name="numberOfComparisons">Number of statistical tests performed.</param>
    /// <returns>Adjusted p-value.</returns>
    public virtual double BonferroniCorrection(double pValue, int numberOfComparisons)
    {
        if (numberOfComparisons <= 0)
        {
            throw new ArgumentException("Number of comparisons must be positive", nameof(numberOfComparisons));
        }

        var adjustedPValue = pValue * numberOfComparisons;
        return Math.Min(adjustedPValue, 1.0);
    }

    /// <summary>
    /// Calculates effect size (Cohen's d) for paired samples.
    /// </summary>
    /// <param name="sample1">First sample values.</param>
    /// <param name="sample2">Second sample values (must be same length as sample1).</param>
    /// <returns>Cohen's d effect size.</returns>
    public virtual double CalculateCohenD(double[] sample1, double[] sample2)
    {
        ArgumentNullException.ThrowIfNull(sample1);
        ArgumentNullException.ThrowIfNull(sample2);

        if (sample1.Length != sample2.Length)
        {
            throw new ArgumentException("Samples must have the same length");
        }

        if (sample1.Length < 2)
        {
            return 0.0;
        }

        var mean1 = sample1.Average();
        var mean2 = sample2.Average();

        var variance1 = sample1.Sum(x => Math.Pow(x - mean1, 2)) / (sample1.Length - 1);
        var variance2 = sample2.Sum(x => Math.Pow(x - mean2, 2)) / (sample2.Length - 1);

        var pooledStdDev = Math.Sqrt((variance1 + variance2) / 2.0);

        return pooledStdDev > 0 ? (mean1 - mean2) / pooledStdDev : 0.0;
    }

    /// <summary>
    /// Calculates two-tailed p-value from t-statistic using approximation.
    /// Uses the Student's t-distribution approximation based on degrees of freedom.
    /// </summary>
    private double CalculateTwoTailedPValue(double tStatistic, int degreesOfFreedom)
    {
        // For very large samples, use normal approximation
        if (degreesOfFreedom > 100)
        {
            return 2.0 * (1.0 - ApproximateStandardNormalCDF(tStatistic));
        }

        // For smaller samples, use t-distribution approximation
        // This is a simplified approximation - for production, consider using MathNet.Numerics
        var x = degreesOfFreedom / (degreesOfFreedom + tStatistic * tStatistic);
        var pValue = IncompleteBetaApproximation(degreesOfFreedom / 2.0, 0.5, x);

        return Math.Min(Math.Max(pValue, 0.0), 1.0);
    }

    /// <summary>
    /// Approximates the standard normal cumulative distribution function.
    /// </summary>
    private double ApproximateStandardNormalCDF(double x)
    {
        // Approximation of the cumulative distribution function for standard normal
        // Using the error function approximation
        var t = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
        var d = 0.3989423 * Math.Exp(-x * x / 2.0);
        var prob = d * t * (0.3193815 + t * (-0.3565638 + t * (1.781478 + t * (-1.821256 + t * 1.330274))));

        return x > 0 ? 1.0 - prob : prob;
    }

    /// <summary>
    /// Approximates the incomplete beta function for p-value calculation.
    /// </summary>
    private double IncompleteBetaApproximation(double a, double b, double x)
    {
        if (x <= 0.0) return 0.0;
        if (x >= 1.0) return 1.0;

        // Simple approximation - for production use, consider MathNet.Numerics
        // This uses a series expansion for the incomplete beta function
        var bt = Math.Exp(
            LogGamma(a + b) - LogGamma(a) - LogGamma(b) +
            a * Math.Log(x) + b * Math.Log(1.0 - x)
        );

        if (x < (a + 1.0) / (a + b + 2.0))
        {
            return bt * BetaContinuedFraction(a, b, x) / a;
        }
        else
        {
            return 1.0 - bt * BetaContinuedFraction(b, a, 1.0 - x) / b;
        }
    }

    /// <summary>
    /// Continued fraction expansion for incomplete beta function.
    /// </summary>
    private double BetaContinuedFraction(double a, double b, double x)
    {
        const int maxIterations = 100;
        const double epsilon = 1e-10;

        var qab = a + b;
        var qap = a + 1.0;
        var qam = a - 1.0;
        var c = 1.0;
        var d = 1.0 - qab * x / qap;

        if (Math.Abs(d) < epsilon) d = epsilon;
        d = 1.0 / d;
        var h = d;

        for (int m = 1; m <= maxIterations; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < epsilon) d = epsilon;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < epsilon) c = epsilon;
            d = 1.0 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < epsilon) d = epsilon;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < epsilon) c = epsilon;
            d = 1.0 / d;
            var del = d * c;
            h *= del;

            if (Math.Abs(del - 1.0) < epsilon) break;
        }

        return h;
    }

    /// <summary>
    /// Approximates the log gamma function.
    /// </summary>
    private double LogGamma(double x)
    {
        // Lanczos approximation
        var coefficients = new[]
        {
            76.18009172947146,
            -86.50532032941677,
            24.01409824083091,
            -1.231739572450155,
            0.1208650973866179e-2,
            -0.5395239384953e-5
        };

        var y = x;
        var tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        var ser = 1.000000000190015;

        for (int j = 0; j < coefficients.Length; j++)
        {
            ser += coefficients[j] / ++y;
        }

        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }
}
