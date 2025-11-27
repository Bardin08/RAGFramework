using System.Text;
using System.Text.Json;

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
