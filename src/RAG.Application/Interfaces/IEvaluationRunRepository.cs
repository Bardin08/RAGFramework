using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository interface for evaluation run persistence operations.
/// </summary>
public interface IEvaluationRunRepository
{
    /// <summary>
    /// Creates a new evaluation run.
    /// </summary>
    Task<EvaluationRun> CreateAsync(EvaluationRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an evaluation run by ID.
    /// </summary>
    Task<EvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing evaluation run.
    /// </summary>
    Task<EvaluationRun> UpdateAsync(EvaluationRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all runs for an evaluation, ordered by date descending.
    /// </summary>
    Task<IReadOnlyList<EvaluationRun>> GetRunsAsync(
        Guid? evaluationId = null,
        string? tenantId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run count for pagination.
    /// </summary>
    Task<int> GetRunCountAsync(
        Guid? evaluationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores metric records for a run.
    /// </summary>
    Task AddMetricsAsync(
        IEnumerable<EvaluationMetricRecord> metrics,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metrics for a specific run.
    /// </summary>
    Task<IReadOnlyList<EvaluationMetricRecord>> GetMetricsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metric history across multiple runs.
    /// </summary>
    Task<IReadOnlyList<MetricHistoryPoint>> GetMetricHistoryAsync(
        string metricName,
        Guid? evaluationId = null,
        string? tenantId = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        int maxPoints = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes runs older than the specified date.
    /// </summary>
    Task<int> DeleteOldRunsAsync(
        DateTimeOffset olderThan,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps only the N most recent runs, deleting older ones.
    /// </summary>
    Task<int> KeepRecentRunsAsync(
        int keepCount,
        Guid? evaluationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A point in the metric history time series.
/// </summary>
public record MetricHistoryPoint(
    Guid RunId,
    string MetricName,
    decimal Value,
    DateTimeOffset RecordedAt);
