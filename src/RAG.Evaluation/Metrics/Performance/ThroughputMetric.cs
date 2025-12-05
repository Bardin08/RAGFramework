using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Performance;

/// <summary>
/// Calculates throughput (queries per second).
/// This metric is typically calculated at the report level rather than per-query.
/// </summary>
public class ThroughputMetric : IEvaluationMetric
{
    private readonly QueryTimingTracker? _tracker;

    public ThroughputMetric(QueryTimingTracker? tracker = null)
    {
        _tracker = tracker;
    }

    public string Name => "Throughput";
    public string Description => "Queries processed per second";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        // If we have a tracker, return current throughput
        if (_tracker is not null)
        {
            return Task.FromResult(_tracker.CurrentThroughput);
        }

        // Try to get from context parameters
        if (context.Parameters.TryGetValue("Throughput", out var throughputObj) &&
            throughputObj is double throughput)
        {
            return Task.FromResult(throughput);
        }

        return Task.FromResult(double.NaN);
    }
}
