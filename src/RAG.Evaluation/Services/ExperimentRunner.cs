using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RAG.Evaluation.Experiments;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Services;

/// <summary>
/// Orchestrates A/B testing experiments by running multiple configuration variants
/// and comparing their results using statistical testing.
/// </summary>
public class ExperimentRunner
{
    private readonly EvaluationRunner _evaluationRunner;
    private readonly StatisticalTestService _statisticalTest;
    private readonly ILogger<ExperimentRunner> _logger;

    /// <summary>
    /// Default alpha level for statistical significance testing.
    /// </summary>
    private const double AlphaLevel = 0.05;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentRunner"/> class.
    /// </summary>
    public ExperimentRunner(
        EvaluationRunner evaluationRunner,
        StatisticalTestService statisticalTest,
        ILogger<ExperimentRunner> logger)
    {
        _evaluationRunner = evaluationRunner ?? throw new ArgumentNullException(nameof(evaluationRunner));
        _statisticalTest = statisticalTest ?? throw new ArgumentNullException(nameof(statisticalTest));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs a configuration experiment across all variants.
    /// </summary>
    /// <param name="experiment">The experiment configuration.</param>
    /// <param name="dataset">The evaluation dataset to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Experiment results including statistical comparisons.</returns>
    public async Task<ExperimentResults> RunExperimentAsync(
        ConfigurationExperiment experiment,
        EvaluationDataset dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentNullException.ThrowIfNull(dataset);

        if (!experiment.IsValid())
        {
            throw new ArgumentException("Invalid experiment configuration", nameof(experiment));
        }

        _logger.LogInformation(
            "Starting experiment: {ExperimentName} with {VariantCount} variants on dataset {DatasetName}",
            experiment.Name,
            experiment.Variants.Count,
            dataset.Name);

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var results = new ExperimentResults
        {
            Experiment = experiment,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow
        };

        // Run each variant
        foreach (var variant in experiment.Variants)
        {
            _logger.LogInformation("Running variant: {VariantName}", variant.Name);

            var variantConfig = variant.MergeWithBase(experiment.BaseConfiguration);
            var report = await RunVariantAsync(dataset, variantConfig, cancellationToken);

            var variantResult = new VariantResult
            {
                VariantName = variant.Name,
                Configuration = variantConfig,
                Report = report
            };

            // Extract metric values
            ExtractMetricValues(variantResult, experiment.Metrics);

            // Calculate composite score
            variantResult.CompositeScore = CalculateCompositeScore(variantResult.MetricValues);

            results.VariantResults[variant.Name] = variantResult;

            _logger.LogInformation(
                "Variant {VariantName} completed: CompositeScore={CompositeScore:F4}",
                variant.Name,
                variantResult.CompositeScore);
        }

        // Perform statistical comparisons
        PerformStatisticalComparisons(results, experiment.Metrics);

        // Select winner
        SelectWinner(results);

        stopwatch.Stop();
        results.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Experiment completed: {ExperimentName}, Duration={Duration}ms, Winner={WinnerName}",
            experiment.Name,
            stopwatch.ElapsedMilliseconds,
            results.WinnerVariantName ?? "None");

        return results;
    }

    /// <summary>
    /// Runs evaluation for a single variant configuration.
    /// </summary>
    private async Task<EvaluationReport> RunVariantAsync(
        EvaluationDataset dataset,
        Dictionary<string, object> configuration,
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would configure the RAG system
        // with the variant parameters before running evaluation
        // For now, we pass the configuration through the dataset metadata

        var configuredDataset = new EvaluationDataset
        {
            Name = dataset.Name,
            Description = dataset.Description,
            Version = dataset.Version,
            Samples = dataset.Samples,
            Metadata = new Dictionary<string, object>(dataset.Metadata)
            {
                ["VariantConfiguration"] = configuration
            }
        };

        return await _evaluationRunner.RunAsync(configuredDataset, cancellationToken);
    }

    /// <summary>
    /// Extracts metric values from the evaluation report.
    /// </summary>
    private void ExtractMetricValues(VariantResult result, List<string> requestedMetrics)
    {
        foreach (var metricName in requestedMetrics)
        {
            if (result.Report.Statistics.TryGetValue(metricName, out var stats))
            {
                result.MetricValues[metricName] = stats.Mean;
            }
            else
            {
                _logger.LogWarning(
                    "Metric {MetricName} not found in results for variant {VariantName}",
                    metricName,
                    result.VariantName);
                result.MetricValues[metricName] = 0.0;
            }
        }
    }

    /// <summary>
    /// Calculates composite score using weighted formula:
    /// CompositeScore = 0.15*Precision@10 + 0.15*Recall@10 + 0.2*MRR + 0.4*F1 - 0.1*(ResponseTime_p95/1000)
    /// </summary>
    private double CalculateCompositeScore(Dictionary<string, double> metrics)
    {
        var score = 0.0;

        // Retrieval metrics
        if (metrics.TryGetValue("precision@10", out var precision))
            score += 0.15 * precision;

        if (metrics.TryGetValue("recall@10", out var recall))
            score += 0.15 * recall;

        if (metrics.TryGetValue("mrr", out var mrr))
            score += 0.2 * mrr;

        // Generation metric
        if (metrics.TryGetValue("f1", out var f1))
            score += 0.4 * f1;

        // Performance penalty (normalized to seconds)
        if (metrics.TryGetValue("response_time_p95", out var responseTime))
            score -= 0.1 * (responseTime / 1000.0);

        return score;
    }

    /// <summary>
    /// Performs pairwise statistical comparisons between all variants.
    /// </summary>
    private void PerformStatisticalComparisons(ExperimentResults results, List<string> metrics)
    {
        var variants = results.VariantResults.Values.ToList();

        // Perform pairwise comparisons
        for (int i = 0; i < variants.Count; i++)
        {
            for (int j = i + 1; j < variants.Count; j++)
            {
                var variantA = variants[i];
                var variantB = variants[j];

                foreach (var metricName in metrics)
                {
                    var comparison = CompareVariants(variantA, variantB, metricName);
                    if (comparison != null)
                    {
                        results.Comparisons.Add(comparison);
                    }
                }
            }
        }

        // Apply Bonferroni correction
        var numberOfComparisons = results.Comparisons.Count;
        if (numberOfComparisons > 0)
        {
            _logger.LogInformation(
                "Applying Bonferroni correction for {Count} comparisons",
                numberOfComparisons);

            for (int i = 0; i < results.Comparisons.Count; i++)
            {
                var comparison = results.Comparisons[i];
                var adjustedPValue = _statisticalTest.BonferroniCorrection(
                    comparison.PValue,
                    numberOfComparisons);

                // Update significance based on adjusted p-value
                var isSignificant = adjustedPValue < AlphaLevel;

                // Create updated comparison with adjusted significance
                results.Comparisons[i] = new ComparisonResult
                {
                    VariantA = comparison.VariantA,
                    VariantB = comparison.VariantB,
                    Metric = comparison.Metric,
                    TStatistic = comparison.TStatistic,
                    PValue = adjustedPValue,
                    IsSignificant = isSignificant,
                    EffectSize = comparison.EffectSize
                };
            }
        }
    }

    /// <summary>
    /// Compares two variants on a specific metric using paired t-test.
    /// </summary>
    private ComparisonResult? CompareVariants(
        VariantResult variantA,
        VariantResult variantB,
        string metricName)
    {
        // Get individual sample values for the metric
        var samplesA = GetMetricSamples(variantA.Report, metricName);
        var samplesB = GetMetricSamples(variantB.Report, metricName);

        if (samplesA.Length == 0 || samplesB.Length == 0)
        {
            _logger.LogWarning(
                "Insufficient samples for comparison: {VariantA} vs {VariantB} on {Metric}",
                variantA.VariantName,
                variantB.VariantName,
                metricName);
            return null;
        }

        // Ensure same length for paired test
        var minLength = Math.Min(samplesA.Length, samplesB.Length);
        var pairedA = samplesA.Take(minLength).ToArray();
        var pairedB = samplesB.Take(minLength).ToArray();

        var (tStatistic, pValue) = _statisticalTest.PairedTTest(pairedA, pairedB);
        var effectSize = _statisticalTest.CalculateCohenD(pairedA, pairedB);

        return new ComparisonResult
        {
            VariantA = variantA.VariantName,
            VariantB = variantB.VariantName,
            Metric = metricName,
            TStatistic = tStatistic,
            PValue = pValue,
            IsSignificant = pValue < AlphaLevel,
            EffectSize = effectSize
        };
    }

    /// <summary>
    /// Extracts individual sample values for a metric from the report.
    /// </summary>
    private double[] GetMetricSamples(EvaluationReport report, string metricName)
    {
        return report.Results
            .Where(r => r.MetricName == metricName && r.IsSuccess)
            .Select(r => r.Value)
            .ToArray();
    }

    /// <summary>
    /// Selects the winning variant based on composite score.
    /// </summary>
    private void SelectWinner(ExperimentResults results)
    {
        if (results.VariantResults.Count == 0)
        {
            _logger.LogWarning("No variants to select winner from");
            return;
        }

        var winner = results.VariantResults.Values
            .OrderByDescending(v => v.CompositeScore)
            .First();

        winner.IsWinner = true;
        results.WinnerVariantName = winner.VariantName;

        _logger.LogInformation(
            "Selected winner: {WinnerName} with composite score {Score:F4}",
            winner.VariantName,
            winner.CompositeScore);
    }
}
