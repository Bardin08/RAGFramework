namespace RAG.Evaluation.Metrics.Retrieval;

/// <summary>
/// Aggregates retrieval metrics across multiple queries.
/// Supports both micro-averaging and macro-averaging.
/// </summary>
public class RetrievalMetricsAggregator
{
    /// <summary>
    /// Aggregation strategy.
    /// </summary>
    public enum AggregationStrategy
    {
        /// <summary>
        /// Macro-averaging: Average of per-query metrics.
        /// </summary>
        Macro,

        /// <summary>
        /// Micro-averaging: Global TP/FP/FN across all queries.
        /// </summary>
        Micro
    }

    /// <summary>
    /// Computes macro-averaged metrics (average of per-query values).
    /// </summary>
    public static double MacroAverage(IEnumerable<double> values)
    {
        var list = values.Where(v => !double.IsNaN(v)).ToList();
        return list.Count == 0 ? 0.0 : list.Average();
    }

    /// <summary>
    /// Computes micro-averaged precision given global counts.
    /// </summary>
    public static double MicroPrecision(int totalRelevantRetrieved, int totalRetrieved) =>
        totalRetrieved == 0 ? 0.0 : (double)totalRelevantRetrieved / totalRetrieved;

    /// <summary>
    /// Computes micro-averaged recall given global counts.
    /// </summary>
    public static double MicroRecall(int totalRelevantRetrieved, int totalRelevant) =>
        totalRelevant == 0 ? 0.0 : (double)totalRelevantRetrieved / totalRelevant;

    /// <summary>
    /// Computes F1 score from precision and recall.
    /// </summary>
    public static double F1Score(double precision, double recall) =>
        precision + recall == 0 ? 0.0 : 2 * precision * recall / (precision + recall);

    /// <summary>
    /// Calculates aggregated retrieval statistics from a list of per-query results.
    /// </summary>
    public static RetrievalAggregateResult Aggregate(
        IEnumerable<QueryRetrievalResult> results,
        AggregationStrategy strategy = AggregationStrategy.Macro)
    {
        var list = results.ToList();

        if (list.Count == 0)
            return new RetrievalAggregateResult();

        if (strategy == AggregationStrategy.Macro)
        {
            return new RetrievalAggregateResult
            {
                Precision = MacroAverage(list.Select(r => r.Precision)),
                Recall = MacroAverage(list.Select(r => r.Recall)),
                F1 = MacroAverage(list.Select(r => r.F1)),
                MRR = MacroAverage(list.Select(r => r.MRR)),
                QueryCount = list.Count
            };
        }

        // Micro-averaging
        var totalRelevantRetrieved = list.Sum(r => r.RelevantRetrieved);
        var totalRetrieved = list.Sum(r => r.TotalRetrieved);
        var totalRelevant = list.Sum(r => r.TotalRelevant);

        var microPrecision = MicroPrecision(totalRelevantRetrieved, totalRetrieved);
        var microRecall = MicroRecall(totalRelevantRetrieved, totalRelevant);

        return new RetrievalAggregateResult
        {
            Precision = microPrecision,
            Recall = microRecall,
            F1 = F1Score(microPrecision, microRecall),
            MRR = MacroAverage(list.Select(r => r.MRR)), // MRR is typically macro-averaged
            QueryCount = list.Count
        };
    }
}

/// <summary>
/// Per-query retrieval result for aggregation.
/// </summary>
public record QueryRetrievalResult
{
    public double Precision { get; init; }
    public double Recall { get; init; }
    public double F1 { get; init; }
    public double MRR { get; init; }
    public int RelevantRetrieved { get; init; }
    public int TotalRetrieved { get; init; }
    public int TotalRelevant { get; init; }
}

/// <summary>
/// Aggregated retrieval metrics.
/// </summary>
public record RetrievalAggregateResult
{
    public double Precision { get; init; }
    public double Recall { get; init; }
    public double F1 { get; init; }
    public double MRR { get; init; }
    public int QueryCount { get; init; }
}
