using System.Text;
using System.Text.Json;
using RAG.Tests.Benchmarks.Metrics;

namespace RAG.Tests.Benchmarks.Exporters;

/// <summary>
/// Exports benchmark results to various formats.
/// </summary>
public static class ResultsExporter
{
    /// <summary>
    /// Exports benchmark metrics to CSV format.
    /// </summary>
    /// <param name="metrics">Dictionary of metrics keyed by "Strategy|QueryType".</param>
    /// <param name="filePath">Path to the output CSV file.</param>
    public static void ExportToCsv(Dictionary<string, BenchmarkMetrics> metrics, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        // Write header
        writer.WriteLine("Strategy,QueryType,QueryCount,Precision@5,Precision@10,Recall@5,Recall@10,MRR,P50_ms,P95_ms,P99_ms");

        // Write data rows
        foreach (var (key, metric) in metrics.OrderBy(m => m.Key))
        {
            var parts = key.Split('|');
            var strategy = parts[0];
            var queryType = parts.Length > 1 ? parts[1] : "Overall";

            writer.WriteLine(
                $"{strategy},{queryType},{metric.QueryCount}," +
                $"{metric.Precision5:F4},{metric.Precision10:F4}," +
                $"{metric.Recall5:F4},{metric.Recall10:F4}," +
                $"{metric.MRR:F4}," +
                $"{metric.P50Ms:F2},{metric.P95Ms:F2},{metric.P99Ms:F2}");
        }
    }

    /// <summary>
    /// Exports benchmark metrics to JSON format.
    /// </summary>
    /// <param name="metrics">Dictionary of metrics keyed by "Strategy|QueryType".</param>
    /// <param name="filePath">Path to the output JSON file.</param>
    public static void ExportToJson(Dictionary<string, BenchmarkMetrics> metrics, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Transform metrics into a structured format
        var results = metrics.Select(kvp =>
        {
            var parts = kvp.Key.Split('|');
            return new
            {
                Strategy = parts[0],
                QueryType = parts.Length > 1 ? parts[1] : "Overall",
                QueryCount = kvp.Value.QueryCount,
                Precision = new
                {
                    At5 = kvp.Value.Precision5,
                    At10 = kvp.Value.Precision10
                },
                Recall = new
                {
                    At5 = kvp.Value.Recall5,
                    At10 = kvp.Value.Recall10
                },
                MRR = kvp.Value.MRR,
                Latency = new
                {
                    P50_ms = kvp.Value.P50Ms,
                    P95_ms = kvp.Value.P95Ms,
                    P99_ms = kvp.Value.P99Ms
                }
            };
        }).OrderBy(r => r.Strategy).ThenBy(r => r.QueryType).ToList();

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(new
        {
            Timestamp = DateTime.UtcNow,
            Results = results
        }, jsonOptions);

        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Exports comparison table with statistical significance testing.
    /// Compares all strategies against BM25 baseline and includes delta percentages and p-values.
    /// </summary>
    /// <param name="metrics">Dictionary of metrics keyed by "Strategy|QueryType".</param>
    /// <param name="rawResults">Raw per-query results for statistical testing (Strategy -> List of precision scores).</param>
    /// <param name="filePath">Path to the output comparison CSV file.</param>
    public static void ExportComparisonTable(
        Dictionary<string, BenchmarkMetrics> metrics,
        Dictionary<string, List<double>> rawResults,
        string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        // Write header
        writer.WriteLine("Strategy,Precision@5,Δ vs BM25,p-value,Significance,Recall@5,MRR,Latency_P95_ms");

        // Get overall metrics for each strategy
        var overallMetrics = metrics
            .Where(m => m.Key.Contains("|Overall"))
            .OrderBy(m => m.Key)
            .ToList();

        // Get BM25 baseline
        var bm25Overall = overallMetrics.FirstOrDefault(m => m.Key.StartsWith("BM25"));
        if (bm25Overall.Key == null)
        {
            writer.WriteLine("# Error: BM25 baseline not found");
            return;
        }

        var bm25Precision = bm25Overall.Value.Precision5;
        var bm25RawScores = rawResults.ContainsKey("BM25") ? rawResults["BM25"].ToArray() : Array.Empty<double>();

        // Write rows for each strategy
        foreach (var (key, metric) in overallMetrics)
        {
            var strategy = key.Split('|')[0];
            var precision5 = metric.Precision5;
            var delta = StatisticalTests.CalculateImprovementPercentage(bm25Precision, precision5);

            string pValueStr = "n/a";
            string significance = "";

            // Perform t-test if not BM25 and raw results available
            if (strategy != "BM25" && rawResults.ContainsKey(strategy) && bm25RawScores.Length > 0)
            {
                var strategyRawScores = rawResults[strategy].ToArray();
                if (strategyRawScores.Length == bm25RawScores.Length && strategyRawScores.Length >= 2)
                {
                    try
                    {
                        var (_, pValue, isSignificant) = StatisticalTests.PairedTTest(strategyRawScores, bm25RawScores);
                        pValueStr = pValue < 0.001 ? "<0.001" : $"{pValue:F4}";
                        significance = StatisticalTests.GetSignificanceIndicator(pValue);
                    }
                    catch
                    {
                        pValueStr = "error";
                    }
                }
            }

            var deltaStr = strategy == "BM25" ? "-" : $"{delta:+0.0;-0.0}%";

            writer.WriteLine(
                $"{strategy},{precision5:F4},{deltaStr},{pValueStr},{significance}," +
                $"{metric.Recall5:F4},{metric.MRR:F4},{metric.P95Ms:F2}");
        }

        // Add significance legend
        writer.WriteLine();
        writer.WriteLine("# Significance: * p<0.05, ** p<0.01, *** p<0.001");
    }

    /// <summary>
    /// Gets timestamped filename for benchmark results.
    /// </summary>
    /// <param name="baseFileName">Base filename without extension.</param>
    /// <param name="extension">File extension (e.g., ".csv", ".json").</param>
    /// <returns>Timestamped filename.</returns>
    public static string GetTimestampedFileName(string baseFileName, string extension)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"{baseFileName}_{timestamp}{extension}";
    }
}
