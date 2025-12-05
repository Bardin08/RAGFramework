using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Performance;

/// <summary>
/// Tracks response time for evaluation queries.
/// Note: This metric requires timing information to be passed via context parameters.
/// </summary>
public class ResponseTimeMetric : IEvaluationMetric
{
    public string Name => "ResponseTimeMs";
    public string Description => "Total response time in milliseconds";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        // Try to get timing from context parameters
        if (context.Parameters.TryGetValue("ResponseTimeMs", out var responseTimeObj) &&
            responseTimeObj is double responseTime)
        {
            return Task.FromResult(responseTime);
        }

        // Alternative: try to parse from string
        if (context.Parameters.TryGetValue("ResponseTimeMs", out var responseTimeStrObj) &&
            responseTimeStrObj is string responseTimeStr &&
            double.TryParse(responseTimeStr, out var parsedTime))
        {
            return Task.FromResult(parsedTime);
        }

        return Task.FromResult(double.NaN);
    }
}
