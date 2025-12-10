namespace RAG.Evaluation.Export;

using System.Text;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Metrics.Performance;
using RAG.Evaluation.Models;

/// <summary>
/// Exports evaluation results to CSV format.
/// </summary>
public class CsvResultsExporter : IResultsExporter
{
    public string Format => "CSV";
    public string ContentType => "text/csv";
    public string FileExtension => "csv";

    /// <inheritdoc/>
    public Task<byte[]> ExportAsync(EvaluationReport report, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var csv = new StringBuilder();

        // Add header comments
        csv.AppendLine($"# Evaluation Report: {options.DatasetName ?? report.RunId.ToString()}");
        csv.AppendLine($"# Run ID: {report.RunId}");
        csv.AppendLine($"# Started: {report.StartedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"# Completed: {report.CompletedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"# Duration: {report.Duration.TotalSeconds:F2}s");
        csv.AppendLine($"# Sample Count: {report.SampleCount}");

        if (options.IncludeConfiguration && report.Configuration.Count > 0)
        {
            csv.AppendLine("#");
            csv.AppendLine("# Configuration:");
            foreach (var (key, value) in report.Configuration)
            {
                csv.AppendLine($"#   {key}: {value}");
            }
        }

        csv.AppendLine("#");

        if (options.IncludePerQueryBreakdown)
        {
            ExportPerQueryBreakdown(csv, report, options);
        }
        else
        {
            ExportAggregated(csv, report, options);
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    /// <inheritdoc/>
    public Task<byte[]> ExportComparisonAsync(List<EvaluationReport> reports, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var csv = new StringBuilder();

        // Add header
        csv.AppendLine("# Evaluation Comparison Report");
        csv.AppendLine($"# Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"# Number of Reports: {reports.Count}");
        csv.AppendLine("#");

        // Get all unique metrics
        var allMetrics = reports
            .SelectMany(r => r.Statistics.Keys)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        if (options.MetricsToInclude != null)
        {
            allMetrics = allMetrics.Intersect(options.MetricsToInclude).ToList();
        }

        // Build header row
        var header = new StringBuilder("Metric");
        for (int i = 0; i < reports.Count; i++)
        {
            var runId = reports[i].RunId.ToString()[..8];
            header.Append($",Run_{i + 1}_Mean,Run_{i + 1}_StdDev");
            if (options.IncludePercentiles)
            {
                header.Append($",Run_{i + 1}_P50,Run_{i + 1}_P95,Run_{i + 1}_P99");
            }
        }
        csv.AppendLine(header.ToString());

        // Build data rows
        foreach (var metric in allMetrics)
        {
            var row = new StringBuilder(EscapeCsvValue(metric));

            foreach (var report in reports)
            {
                if (report.Statistics.TryGetValue(metric, out var stats))
                {
                    row.Append($",{stats.Mean:F4},{stats.StandardDeviation:F4}");

                    if (options.IncludePercentiles)
                    {
                        var values = GetMetricValues(report, metric);
                        var p50 = PercentileCalculator.P50(values);
                        var p95 = PercentileCalculator.P95(values);
                        var p99 = PercentileCalculator.P99(values);
                        row.Append($",{p50:F4},{p95:F4},{p99:F4}");
                    }
                }
                else
                {
                    var emptyCols = options.IncludePercentiles ? 5 : 2;
                    row.Append(string.Concat(Enumerable.Repeat(",", emptyCols)));
                }
            }

            csv.AppendLine(row.ToString());
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    private void ExportAggregated(StringBuilder csv, EvaluationReport report, ExportOptions options)
    {
        // Header row
        var header = "Metric,Mean,Std Dev,Min,Max";
        if (options.IncludePercentiles)
        {
            header += ",P50,P95,P99";
        }
        header += ",Success Count,Failure Count";
        csv.AppendLine(header);

        // Get metrics to export
        var metrics = options.MetricsToInclude != null
            ? report.Statistics.Keys.Intersect(options.MetricsToInclude).OrderBy(m => m)
            : report.Statistics.Keys.OrderBy(m => m);

        // Data rows
        foreach (var metricName in metrics)
        {
            if (!report.Statistics.TryGetValue(metricName, out var stats))
                continue;

            var row = new StringBuilder();
            row.Append(EscapeCsvValue(metricName));
            row.Append($",{stats.Mean:F4}");
            row.Append($",{stats.StandardDeviation:F4}");
            row.Append($",{stats.Min:F4}");
            row.Append($",{stats.Max:F4}");

            if (options.IncludePercentiles)
            {
                var values = GetMetricValues(report, metricName);
                var p50 = PercentileCalculator.P50(values);
                var p95 = PercentileCalculator.P95(values);
                var p99 = PercentileCalculator.P99(values);
                row.Append($",{p50:F4},{p95:F4},{p99:F4}");
            }

            row.Append($",{stats.SuccessCount},{stats.FailureCount}");
            csv.AppendLine(row.ToString());
        }
    }

    private void ExportPerQueryBreakdown(StringBuilder csv, EvaluationReport report, ExportOptions options)
    {
        // Get all unique metric names
        var metricNames = report.Results
            .Select(r => r.MetricName)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        if (options.MetricsToInclude != null)
        {
            metricNames = metricNames.Intersect(options.MetricsToInclude).ToList();
        }

        // Header row
        var header = new StringBuilder("Query Index");
        foreach (var metric in metricNames)
        {
            header.Append($",{EscapeCsvValue(metric)}");
        }
        csv.AppendLine(header.ToString());

        // Group results by query (using metadata if available)
        var groupedResults = report.Results
            .Where(r => metricNames.Contains(r.MetricName))
            .GroupBy(r => GetQueryIndex(r))
            .OrderBy(g => g.Key);

        foreach (var group in groupedResults)
        {
            var row = new StringBuilder();
            row.Append(group.Key);

            foreach (var metricName in metricNames)
            {
                var result = group.FirstOrDefault(r => r.MetricName == metricName);
                if (result != null && result.IsSuccess)
                {
                    row.Append($",{result.Value:F4}");
                }
                else
                {
                    row.Append(",");
                }
            }

            csv.AppendLine(row.ToString());
        }
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static IEnumerable<double> GetMetricValues(EvaluationReport report, string metricName)
    {
        return report.Results
            .Where(r => r.MetricName == metricName && r.IsSuccess)
            .Select(r => r.Value);
    }

    private static int GetQueryIndex(EvaluationResult result)
    {
        if (result.Metadata.TryGetValue("QueryIndex", out var indexObj) && indexObj is int index)
        {
            return index;
        }
        if (result.Metadata.TryGetValue("SampleIndex", out var sampleObj) && sampleObj is int sample)
        {
            return sample;
        }
        return result.GetHashCode();
    }
}
