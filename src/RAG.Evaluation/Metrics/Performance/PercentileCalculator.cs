namespace RAG.Evaluation.Metrics.Performance;

/// <summary>
/// Utility for calculating percentiles and statistical measures.
/// </summary>
public static class PercentileCalculator
{
    /// <summary>
    /// Calculates a percentile using linear interpolation.
    /// </summary>
    /// <param name="data">The data points (will be sorted).</param>
    /// <param name="percentile">The percentile to calculate (0-100).</param>
    /// <returns>The percentile value.</returns>
    public static double CalculatePercentile(IEnumerable<double> data, double percentile)
    {
        var sorted = data.OrderBy(x => x).ToList();

        if (sorted.Count == 0)
            return double.NaN;

        if (sorted.Count == 1)
            return sorted[0];

        if (percentile <= 0)
            return sorted[0];

        if (percentile >= 100)
            return sorted[^1];

        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
            return sorted[lower];

        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    /// <summary>
    /// Calculates the 50th percentile (median).
    /// </summary>
    public static double P50(IEnumerable<double> data) => CalculatePercentile(data, 50);

    /// <summary>
    /// Calculates the 95th percentile.
    /// </summary>
    public static double P95(IEnumerable<double> data) => CalculatePercentile(data, 95);

    /// <summary>
    /// Calculates the 99th percentile.
    /// </summary>
    public static double P99(IEnumerable<double> data) => CalculatePercentile(data, 99);

    /// <summary>
    /// Calculates the mean (average).
    /// </summary>
    public static double Mean(IEnumerable<double> data)
    {
        var list = data.ToList();
        return list.Count == 0 ? double.NaN : list.Average();
    }

    /// <summary>
    /// Calculates the standard deviation.
    /// </summary>
    public static double StandardDeviation(IEnumerable<double> data)
    {
        var list = data.ToList();
        if (list.Count < 2)
            return double.NaN;

        var mean = list.Average();
        var variance = list.Sum(x => Math.Pow(x - mean, 2)) / list.Count;
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Calculates comprehensive statistics for a dataset.
    /// </summary>
    public static PerformanceStatistics CalculateStatistics(IEnumerable<double> data)
    {
        var list = data.ToList();

        if (list.Count == 0)
        {
            return new PerformanceStatistics
            {
                Count = 0,
                Mean = double.NaN,
                Median = double.NaN,
                StandardDeviation = double.NaN,
                Min = double.NaN,
                Max = double.NaN,
                P50 = double.NaN,
                P95 = double.NaN,
                P99 = double.NaN
            };
        }

        var sorted = list.OrderBy(x => x).ToList();

        return new PerformanceStatistics
        {
            Count = list.Count,
            Mean = Mean(sorted),
            Median = P50(sorted),
            StandardDeviation = StandardDeviation(sorted),
            Min = sorted[0],
            Max = sorted[^1],
            P50 = P50(sorted),
            P95 = P95(sorted),
            P99 = P99(sorted)
        };
    }
}

/// <summary>
/// Comprehensive performance statistics.
/// </summary>
public record PerformanceStatistics
{
    public int Count { get; init; }
    public double Mean { get; init; }
    public double Median { get; init; }
    public double StandardDeviation { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
}
