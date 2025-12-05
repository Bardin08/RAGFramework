using System.Collections.Concurrent;
using System.Diagnostics;

namespace RAG.Evaluation.Metrics.Performance;

/// <summary>
/// Tracks timing information for individual queries and their phases.
/// </summary>
public class QueryTimingTracker
{
    private readonly ConcurrentDictionary<string, QueryTiming> _timings = new();
    private readonly Stopwatch _overallStopwatch = new();
    private int _completedQueries;

    /// <summary>
    /// Starts tracking overall evaluation time.
    /// </summary>
    public void Start()
    {
        _overallStopwatch.Restart();
    }

    /// <summary>
    /// Stops overall tracking.
    /// </summary>
    public void Stop()
    {
        _overallStopwatch.Stop();
    }

    /// <summary>
    /// Starts timing for a specific query.
    /// </summary>
    public QueryTiming StartQuery(string queryId)
    {
        var timing = new QueryTiming(queryId);
        timing.Start();
        _timings[queryId] = timing;
        return timing;
    }

    /// <summary>
    /// Gets timing for a specific query.
    /// </summary>
    public QueryTiming? GetQueryTiming(string queryId)
    {
        return _timings.TryGetValue(queryId, out var timing) ? timing : null;
    }

    /// <summary>
    /// Marks a query as complete and increments counter.
    /// </summary>
    public void CompleteQuery(string queryId)
    {
        if (_timings.TryGetValue(queryId, out var timing))
        {
            timing.Stop();
            Interlocked.Increment(ref _completedQueries);
        }
    }

    /// <summary>
    /// Gets the total elapsed time.
    /// </summary>
    public TimeSpan TotalElapsed => _overallStopwatch.Elapsed;

    /// <summary>
    /// Gets the number of completed queries.
    /// </summary>
    public int CompletedQueryCount => _completedQueries;

    /// <summary>
    /// Gets current throughput (queries per second).
    /// </summary>
    public double CurrentThroughput
    {
        get
        {
            var elapsed = _overallStopwatch.Elapsed.TotalSeconds;
            return elapsed > 0 ? _completedQueries / elapsed : 0;
        }
    }

    /// <summary>
    /// Gets all recorded timings.
    /// </summary>
    public IReadOnlyList<QueryTiming> GetAllTimings() => _timings.Values.ToList();

    /// <summary>
    /// Generates a performance report from all recorded timings.
    /// </summary>
    public PerformanceReport GenerateReport()
    {
        var timings = _timings.Values.Where(t => t.TotalMs > 0).ToList();

        if (timings.Count == 0)
        {
            return new PerformanceReport
            {
                TotalDuration = TotalElapsed,
                QueryCount = 0,
                Throughput = 0
            };
        }

        return new PerformanceReport
        {
            TotalDuration = TotalElapsed,
            QueryCount = timings.Count,
            Throughput = CurrentThroughput,
            TotalResponseTime = PercentileCalculator.CalculateStatistics(timings.Select(t => t.TotalMs)),
            RetrievalTime = PercentileCalculator.CalculateStatistics(timings.Select(t => t.RetrievalMs)),
            RerankingTime = PercentileCalculator.CalculateStatistics(timings.Select(t => t.RerankingMs)),
            GenerationTime = PercentileCalculator.CalculateStatistics(timings.Select(t => t.GenerationMs))
        };
    }
}

/// <summary>
/// Timing information for a single query.
/// </summary>
public class QueryTiming
{
    private readonly Stopwatch _totalStopwatch = new();
    private readonly Stopwatch _retrievalStopwatch = new();
    private readonly Stopwatch _rerankingStopwatch = new();
    private readonly Stopwatch _generationStopwatch = new();

    public QueryTiming(string queryId)
    {
        QueryId = queryId;
    }

    public string QueryId { get; }

    public double TotalMs => _totalStopwatch.Elapsed.TotalMilliseconds;
    public double RetrievalMs => _retrievalStopwatch.Elapsed.TotalMilliseconds;
    public double RerankingMs => _rerankingStopwatch.Elapsed.TotalMilliseconds;
    public double GenerationMs => _generationStopwatch.Elapsed.TotalMilliseconds;

    public void Start() => _totalStopwatch.Start();
    public void Stop() => _totalStopwatch.Stop();

    public void StartRetrieval() => _retrievalStopwatch.Start();
    public void StopRetrieval() => _retrievalStopwatch.Stop();

    public void StartReranking() => _rerankingStopwatch.Start();
    public void StopReranking() => _rerankingStopwatch.Stop();

    public void StartGeneration() => _generationStopwatch.Start();
    public void StopGeneration() => _generationStopwatch.Stop();
}

/// <summary>
/// Comprehensive performance report.
/// </summary>
public record PerformanceReport
{
    public TimeSpan TotalDuration { get; init; }
    public int QueryCount { get; init; }
    public double Throughput { get; init; }
    public PerformanceStatistics TotalResponseTime { get; init; } = new();
    public PerformanceStatistics RetrievalTime { get; init; } = new();
    public PerformanceStatistics RerankingTime { get; init; } = new();
    public PerformanceStatistics GenerationTime { get; init; } = new();
}
