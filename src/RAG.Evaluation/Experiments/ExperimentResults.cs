using RAG.Evaluation.Models;

namespace RAG.Evaluation.Experiments;

/// <summary>
/// Results from running a configuration experiment.
/// </summary>
public class ExperimentResults
{
    /// <summary>
    /// The experiment that was run.
    /// </summary>
    public required ConfigurationExperiment Experiment { get; init; }

    /// <summary>
    /// Results for each variant, keyed by variant name.
    /// </summary>
    public Dictionary<string, VariantResult> VariantResults { get; init; } = new();

    /// <summary>
    /// Statistical comparison results between variants.
    /// </summary>
    public List<ComparisonResult> Comparisons { get; init; } = new();

    /// <summary>
    /// Name of the winning variant based on composite score.
    /// </summary>
    public string? WinnerVariantName { get; set; }

    /// <summary>
    /// When the experiment started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the experiment completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>
    /// Total duration of the experiment.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// Results for a single variant in an experiment.
/// </summary>
public class VariantResult
{
    /// <summary>
    /// Name of the variant.
    /// </summary>
    public required string VariantName { get; init; }

    /// <summary>
    /// Configuration used for this variant.
    /// </summary>
    public Dictionary<string, object> Configuration { get; init; } = new();

    /// <summary>
    /// Evaluation report from running the variant.
    /// </summary>
    public required EvaluationReport Report { get; init; }

    /// <summary>
    /// Composite score calculated for this variant.
    /// </summary>
    public double CompositeScore { get; set; }

    /// <summary>
    /// Whether this variant is the winner.
    /// </summary>
    public bool IsWinner { get; set; }

    /// <summary>
    /// Individual metric values extracted from the report.
    /// </summary>
    public Dictionary<string, double> MetricValues { get; init; } = new();
}

/// <summary>
/// Statistical comparison between two variants.
/// </summary>
public class ComparisonResult
{
    /// <summary>
    /// Name of the first variant being compared.
    /// </summary>
    public required string VariantA { get; init; }

    /// <summary>
    /// Name of the second variant being compared.
    /// </summary>
    public required string VariantB { get; init; }

    /// <summary>
    /// The metric being compared.
    /// </summary>
    public required string Metric { get; init; }

    /// <summary>
    /// T-statistic from the paired t-test.
    /// </summary>
    public double TStatistic { get; init; }

    /// <summary>
    /// P-value from the statistical test.
    /// </summary>
    public double PValue { get; init; }

    /// <summary>
    /// Whether the difference is statistically significant.
    /// </summary>
    public bool IsSignificant { get; init; }

    /// <summary>
    /// Effect size (Cohen's d).
    /// </summary>
    public double EffectSize { get; init; }
}
