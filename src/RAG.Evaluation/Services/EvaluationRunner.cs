using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Evaluation.Configuration;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Services;

/// <summary>
/// Orchestrates running evaluation metrics against a dataset.
/// </summary>
public class EvaluationRunner
{
    private readonly IEnumerable<IEvaluationMetric> _metrics;
    private readonly ILogger<EvaluationRunner> _logger;
    private readonly EvaluationOptions _options;

    public EvaluationRunner(
        IEnumerable<IEvaluationMetric> metrics,
        ILogger<EvaluationRunner> logger,
        IOptions<EvaluationOptions> options)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Runs all registered metrics against the provided dataset.
    /// </summary>
    /// <param name="dataset">The evaluation dataset containing samples.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An evaluation report with all results and statistics.</returns>
    public async Task<EvaluationReport> RunAsync(
        EvaluationDataset dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var metricsList = _metrics.ToList();
        if (metricsList.Count == 0)
        {
            _logger.LogWarning("No metrics registered. Returning empty report.");
            return CreateEmptyReport(dataset);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting evaluation run: Dataset={DatasetName}, Samples={SampleCount}, Metrics={MetricCount}",
            dataset.Name,
            dataset.Samples.Count,
            metricsList.Count);

        var results = new ConcurrentBag<EvaluationResult>();
        var metricsToRun = GetMetricsToRun(metricsList);

        await ProcessSamplesAsync(dataset, metricsToRun, results, cancellationToken);

        stopwatch.Stop();
        var completedAt = DateTimeOffset.UtcNow;

        var resultsList = results.ToList();
        var statistics = CalculateStatistics(resultsList, metricsToRun);

        _logger.LogInformation(
            "Evaluation completed: Duration={Duration}ms, TotalResults={ResultCount}",
            stopwatch.ElapsedMilliseconds,
            resultsList.Count);

        LogSummaryStatistics(statistics);

        return new EvaluationReport
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
            SampleCount = dataset.Samples.Count,
            Results = resultsList,
            Statistics = statistics,
            Configuration = new Dictionary<string, object>
            {
                ["DatasetName"] = dataset.Name,
                ["DatasetVersion"] = dataset.Version,
                ["MetricsRun"] = metricsToRun.Select(m => m.Name).ToList(),
                ["MaxParallelism"] = _options.MaxParallelism
            }
        };
    }

    private List<IEvaluationMetric> GetMetricsToRun(List<IEvaluationMetric> allMetrics)
    {
        if (_options.MetricsToRun is null || _options.MetricsToRun.Count == 0)
            return allMetrics;

        return allMetrics
            .Where(m => _options.MetricsToRun.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task ProcessSamplesAsync(
        EvaluationDataset dataset,
        List<IEvaluationMetric> metrics,
        ConcurrentBag<EvaluationResult> results,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            dataset.Samples,
            parallelOptions,
            async (sample, ct) =>
            {
                if (!sample.IsValid())
                {
                    _logger.LogWarning("Skipping invalid sample: {SampleId}", sample.SampleId);
                    return;
                }

                foreach (var metric in metrics)
                {
                    var result = await EvaluateMetricAsync(metric, sample, ct);
                    results.Add(result);
                }
            });
    }

    private async Task<EvaluationResult> EvaluateMetricAsync(
        IEvaluationMetric metric,
        EvaluationContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var value = await metric.CalculateAsync(context, cancellationToken);
            stopwatch.Stop();

            _logger.LogDebug(
                "Metric calculated: {MetricName}={Value:F4}, Sample={SampleId}, Duration={Duration}ms",
                metric.Name,
                value,
                context.SampleId,
                stopwatch.ElapsedMilliseconds);

            return new EvaluationResult(
                metric.Name,
                value,
                DateTimeOffset.UtcNow,
                context.Parameters)
            {
                Metadata = new Dictionary<string, object>
                {
                    ["SampleId"] = context.SampleId ?? "unknown",
                    ["DurationMs"] = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Metric calculation failed: {MetricName}, Sample={SampleId}",
                metric.Name,
                context.SampleId);

            return EvaluationResult.Failure(metric.Name, ex.Message);
        }
    }

    private Dictionary<string, MetricStatistics> CalculateStatistics(
        List<EvaluationResult> results,
        List<IEvaluationMetric> metrics)
    {
        var statistics = new Dictionary<string, MetricStatistics>();

        foreach (var metric in metrics)
        {
            var metricResults = results
                .Where(r => r.MetricName == metric.Name)
                .ToList();

            var successfulValues = metricResults
                .Where(r => r.IsSuccess && !double.IsNaN(r.Value))
                .Select(r => r.Value)
                .ToList();

            if (successfulValues.Count == 0)
            {
                statistics[metric.Name] = new MetricStatistics(
                    metric.Name,
                    Mean: double.NaN,
                    StandardDeviation: double.NaN,
                    Min: double.NaN,
                    Max: double.NaN,
                    SuccessCount: 0,
                    FailureCount: metricResults.Count);
                continue;
            }

            var mean = successfulValues.Average();
            var variance = successfulValues.Sum(v => Math.Pow(v - mean, 2)) / successfulValues.Count;
            var stdDev = Math.Sqrt(variance);

            statistics[metric.Name] = new MetricStatistics(
                metric.Name,
                Mean: mean,
                StandardDeviation: stdDev,
                Min: successfulValues.Min(),
                Max: successfulValues.Max(),
                SuccessCount: successfulValues.Count,
                FailureCount: metricResults.Count - successfulValues.Count);
        }

        return statistics;
    }

    private void LogSummaryStatistics(Dictionary<string, MetricStatistics> statistics)
    {
        foreach (var (metricName, stats) in statistics)
        {
            _logger.LogInformation(
                "Metric summary: {MetricName} - Mean={Mean:F4}, StdDev={StdDev:F4}, " +
                "Min={Min:F4}, Max={Max:F4}, Success={SuccessCount}, Failed={FailedCount}",
                metricName,
                stats.Mean,
                stats.StandardDeviation,
                stats.Min,
                stats.Max,
                stats.SuccessCount,
                stats.FailureCount);
        }
    }

    private static EvaluationReport CreateEmptyReport(EvaluationDataset dataset)
    {
        var now = DateTimeOffset.UtcNow;
        return new EvaluationReport
        {
            StartedAt = now,
            CompletedAt = now,
            SampleCount = dataset.Samples.Count,
            Results = [],
            Statistics = new Dictionary<string, MetricStatistics>(),
            Configuration = new Dictionary<string, object>
            {
                ["DatasetName"] = dataset.Name,
                ["DatasetVersion"] = dataset.Version,
                ["Warning"] = "No metrics were registered"
            }
        };
    }
}
