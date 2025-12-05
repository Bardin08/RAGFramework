using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Services;

/// <summary>
/// Service for managing evaluation results, historical analysis, and comparisons.
/// </summary>
public class EvaluationResultsService
{
    private readonly IEvaluationRunRepository _repository;
    private readonly ILogger<EvaluationResultsService> _logger;

    public EvaluationResultsService(
        IEvaluationRunRepository repository,
        ILogger<EvaluationResultsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new evaluation run in queued status.
    /// </summary>
    public async Task<EvaluationRun> CreateRunAsync(
        string name,
        Guid? evaluationId = null,
        string? tenantId = null,
        string? initiatedBy = null,
        Dictionary<string, object>? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var run = new EvaluationRun
        {
            Name = name,
            EvaluationId = evaluationId,
            TenantId = tenantId,
            InitiatedBy = initiatedBy,
            Status = EvaluationRunStatus.Queued,
            Configuration = configuration is not null
                ? JsonSerializer.Serialize(configuration)
                : "{}"
        };

        return await _repository.CreateAsync(run, cancellationToken);
    }

    /// <summary>
    /// Marks a run as started.
    /// </summary>
    public async Task<EvaluationRun> StartRunAsync(
        Guid runId,
        int totalQueries,
        CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId} not found");

        run.Status = EvaluationRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        run.TotalQueries = totalQueries;
        run.Progress = 0;

        return await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <summary>
    /// Updates run progress.
    /// </summary>
    public async Task UpdateProgressAsync(
        Guid runId,
        int completedQueries,
        int failedQueries = 0,
        CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId} not found");

        run.CompletedQueries = completedQueries;
        run.FailedQueries = failedQueries;
        run.Progress = run.TotalQueries > 0
            ? (int)((completedQueries + failedQueries) * 100.0 / run.TotalQueries)
            : 0;

        await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <summary>
    /// Completes a run with results.
    /// </summary>
    public async Task<EvaluationRun> CompleteRunAsync(
        Guid runId,
        EvaluationReport report,
        CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId} not found");

        run.Status = EvaluationRunStatus.Completed;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.Progress = 100;
        run.ResultsSummary = JsonSerializer.Serialize(CreateResultsSummary(report));

        // Store individual metrics
        var metricRecords = report.Statistics
            .Select(kvp => new EvaluationMetricRecord
            {
                RunId = runId,
                MetricName = kvp.Key,
                MetricValue = (decimal)kvp.Value.Mean,
                Metadata = JsonSerializer.Serialize(new
                {
                    kvp.Value.StandardDeviation,
                    kvp.Value.Min,
                    kvp.Value.Max,
                    kvp.Value.SuccessCount,
                    kvp.Value.FailureCount
                })
            })
            .ToList();

        await _repository.AddMetricsAsync(metricRecords, cancellationToken);
        return await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <summary>
    /// Marks a run as failed.
    /// </summary>
    public async Task<EvaluationRun> FailRunAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId} not found");

        run.Status = EvaluationRunStatus.Failed;
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.ErrorMessage = errorMessage;

        _logger.LogError("Evaluation run {RunId} failed: {Error}", runId, errorMessage);
        return await _repository.UpdateAsync(run, cancellationToken);
    }

    /// <summary>
    /// Gets metric history for trend analysis.
    /// </summary>
    public async Task<MetricHistoryResult> GetMetricHistoryAsync(
        string metricName,
        Guid? evaluationId = null,
        string? tenantId = null,
        int maxPoints = 100,
        CancellationToken cancellationToken = default)
    {
        var history = await _repository.GetMetricHistoryAsync(
            metricName, evaluationId, tenantId, null, null, maxPoints, cancellationToken);

        if (history.Count < 2)
        {
            return new MetricHistoryResult
            {
                MetricName = metricName,
                History = history,
                Trend = "insufficient_data",
                MovingAverage7d = null,
                MovingAverage30d = null
            };
        }

        var values = history.Select(h => (double)h.Value).ToList();
        var trend = DetermineTrend(values);
        var ma7 = CalculateMovingAverage(history, 7);
        var ma30 = CalculateMovingAverage(history, 30);

        return new MetricHistoryResult
        {
            MetricName = metricName,
            History = history,
            Trend = trend,
            MovingAverage7d = ma7,
            MovingAverage30d = ma30
        };
    }

    /// <summary>
    /// Compares two evaluation runs.
    /// </summary>
    public async Task<RunComparisonResult> CompareRunsAsync(
        Guid runId1,
        Guid runId2,
        CancellationToken cancellationToken = default)
    {
        var run1 = await _repository.GetByIdAsync(runId1, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId1} not found");

        var run2 = await _repository.GetByIdAsync(runId2, cancellationToken)
            ?? throw new InvalidOperationException($"Run {runId2} not found");

        var metrics1 = await _repository.GetMetricsForRunAsync(runId1, cancellationToken);
        var metrics2 = await _repository.GetMetricsForRunAsync(runId2, cancellationToken);

        var metricsDict1 = metrics1.ToDictionary(m => m.MetricName, m => m.MetricValue);
        var metricsDict2 = metrics2.ToDictionary(m => m.MetricName, m => m.MetricValue);

        var allMetricNames = metricsDict1.Keys.Union(metricsDict2.Keys).Distinct();

        var differences = new Dictionary<string, MetricDifference>();
        foreach (var name in allMetricNames)
        {
            metricsDict1.TryGetValue(name, out var value1);
            metricsDict2.TryGetValue(name, out var value2);

            var delta = value2 - value1;
            var percentChange = value1 != 0 ? (double)((delta / value1) * 100) : 0;

            differences[name] = new MetricDifference((double)delta, percentChange);
        }

        return new RunComparisonResult
        {
            Run1 = new RunSummary(run1.Id, run1.StartedAt, metricsDict1),
            Run2 = new RunSummary(run2.Id, run2.StartedAt, metricsDict2),
            Differences = differences
        };
    }

    private static Dictionary<string, object> CreateResultsSummary(EvaluationReport report)
    {
        var summary = new Dictionary<string, object>
        {
            ["total_queries"] = report.SampleCount,
            ["duration_ms"] = report.Duration.TotalMilliseconds
        };

        foreach (var (name, stats) in report.Statistics)
        {
            summary[name.ToLowerInvariant().Replace("@", "_at_")] = stats.Mean;
        }

        return summary;
    }

    private static string DetermineTrend(List<double> values)
    {
        if (values.Count < 3)
            return "stable";

        // Simple linear regression
        var n = values.Count;
        var xMean = (n - 1) / 2.0;
        var yMean = values.Average();

        var numerator = 0.0;
        var denominator = 0.0;
        for (var i = 0; i < n; i++)
        {
            numerator += (i - xMean) * (values[i] - yMean);
            denominator += (i - xMean) * (i - xMean);
        }

        var slope = denominator != 0 ? numerator / denominator : 0;

        // Normalize by mean to get relative change
        var relativeSlope = yMean != 0 ? slope / yMean : 0;

        return relativeSlope switch
        {
            > 0.01 => "improving",
            < -0.01 => "declining",
            _ => "stable"
        };
    }

    private static double? CalculateMovingAverage(
        IReadOnlyList<MetricHistoryPoint> history,
        int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var recentPoints = history.Where(h => h.RecordedAt >= cutoff).ToList();

        if (recentPoints.Count == 0)
            return null;

        return recentPoints.Average(p => (double)p.Value);
    }
}

/// <summary>
/// Result of metric history analysis.
/// </summary>
public record MetricHistoryResult
{
    public string MetricName { get; init; } = string.Empty;
    public IReadOnlyList<MetricHistoryPoint> History { get; init; } = [];
    public string Trend { get; init; } = "unknown";
    public double? MovingAverage7d { get; init; }
    public double? MovingAverage30d { get; init; }
}

/// <summary>
/// Result of comparing two runs.
/// </summary>
public record RunComparisonResult
{
    public RunSummary Run1 { get; init; } = null!;
    public RunSummary Run2 { get; init; } = null!;
    public IReadOnlyDictionary<string, MetricDifference> Differences { get; init; } =
        new Dictionary<string, MetricDifference>();
}

/// <summary>
/// Summary of a run for comparison.
/// </summary>
public record RunSummary(
    Guid Id,
    DateTimeOffset Date,
    IReadOnlyDictionary<string, decimal> Metrics);

/// <summary>
/// Difference between metric values.
/// </summary>
public record MetricDifference(double Delta, double PercentChange);
