using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for evaluation run persistence.
/// </summary>
public class EvaluationRunRepository : IEvaluationRunRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EvaluationRunRepository> _logger;

    public EvaluationRunRepository(
        ApplicationDbContext context,
        ILogger<EvaluationRunRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EvaluationRun> CreateAsync(EvaluationRun run, CancellationToken cancellationToken = default)
    {
        _context.EvaluationRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created evaluation run {RunId}", run.Id);
        return run;
    }

    public async Task<EvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EvaluationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<EvaluationRun> UpdateAsync(EvaluationRun run, CancellationToken cancellationToken = default)
    {
        _context.EvaluationRuns.Update(run);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated evaluation run {RunId}, Status={Status}", run.Id, run.Status);
        return run;
    }

    public async Task<IReadOnlyList<EvaluationRun>> GetRunsAsync(
        Guid? evaluationId = null,
        string? tenantId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EvaluationRuns.AsNoTracking();

        if (evaluationId.HasValue)
            query = query.Where(r => r.EvaluationId == evaluationId.Value);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(r => r.TenantId == tenantId);

        return await query
            .OrderByDescending(r => r.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetRunCountAsync(
        Guid? evaluationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EvaluationRuns.AsQueryable();

        if (evaluationId.HasValue)
            query = query.Where(r => r.EvaluationId == evaluationId.Value);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(r => r.TenantId == tenantId);

        return await query.CountAsync(cancellationToken);
    }

    public async Task AddMetricsAsync(
        IEnumerable<EvaluationMetricRecord> metrics,
        CancellationToken cancellationToken = default)
    {
        _context.EvaluationMetricRecords.AddRange(metrics);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added {Count} metric records", metrics.Count());
    }

    public async Task<IReadOnlyList<EvaluationMetricRecord>> GetMetricsForRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EvaluationMetricRecords
            .AsNoTracking()
            .Where(m => m.RunId == runId)
            .OrderBy(m => m.MetricName)
            .ThenBy(m => m.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MetricHistoryPoint>> GetMetricHistoryAsync(
        string metricName,
        Guid? evaluationId = null,
        string? tenantId = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        int maxPoints = 100,
        CancellationToken cancellationToken = default)
    {
        var query = from m in _context.EvaluationMetricRecords
                    join r in _context.EvaluationRuns on m.RunId equals r.Id
                    where m.MetricName == metricName
                    select new { Metric = m, Run = r };

        if (evaluationId.HasValue)
            query = query.Where(x => x.Run.EvaluationId == evaluationId.Value);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(x => x.Run.TenantId == tenantId);

        if (fromDate.HasValue)
            query = query.Where(x => x.Metric.RecordedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.Metric.RecordedAt <= toDate.Value);

        var results = await query
            .OrderByDescending(x => x.Metric.RecordedAt)
            .Take(maxPoints)
            .Select(x => new MetricHistoryPoint(
                x.Run.Id,
                x.Metric.MetricName,
                x.Metric.MetricValue,
                x.Metric.RecordedAt))
            .ToListAsync(cancellationToken);

        // Return in chronological order
        results.Reverse();
        return results;
    }

    public async Task<int> DeleteOldRunsAsync(
        DateTimeOffset olderThan,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EvaluationRuns
            .Where(r => r.StartedAt < olderThan);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(r => r.TenantId == tenantId);

        var runsToDelete = await query.ToListAsync(cancellationToken);

        if (runsToDelete.Count == 0)
            return 0;

        _context.EvaluationRuns.RemoveRange(runsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} old evaluation runs", runsToDelete.Count);
        return runsToDelete.Count;
    }

    public async Task<int> KeepRecentRunsAsync(
        int keepCount,
        Guid? evaluationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EvaluationRuns.AsQueryable();

        if (evaluationId.HasValue)
            query = query.Where(r => r.EvaluationId == evaluationId.Value);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(r => r.TenantId == tenantId);

        var runIdsToKeep = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(keepCount)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var deleteQuery = query.Where(r => !runIdsToKeep.Contains(r.Id));
        var runsToDelete = await deleteQuery.ToListAsync(cancellationToken);

        if (runsToDelete.Count == 0)
            return 0;

        _context.EvaluationRuns.RemoveRange(runsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} runs, keeping {KeepCount} most recent",
            runsToDelete.Count, keepCount);

        return runsToDelete.Count;
    }
}
