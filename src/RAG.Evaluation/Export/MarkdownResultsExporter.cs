namespace RAG.Evaluation.Export;

using System.Text;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Metrics.Performance;
using RAG.Evaluation.Models;

/// <summary>
/// Exports evaluation results to Markdown format.
/// </summary>
public class MarkdownResultsExporter : IResultsExporter
{
    public string Format => "Markdown";
    public string ContentType => "text/markdown";
    public string FileExtension => "md";

    /// <inheritdoc/>
    public Task<byte[]> ExportAsync(EvaluationReport report, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var md = new StringBuilder();

        // Title and metadata
        var title = options.DatasetName ?? $"Evaluation Report {report.RunId.ToString()[..8]}";
        md.AppendLine($"# {title}");
        md.AppendLine();
        md.AppendLine($"**Run ID:** `{report.RunId}`  ");
        md.AppendLine($"**Started:** {report.StartedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        md.AppendLine($"**Completed:** {report.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        md.AppendLine($"**Duration:** {report.Duration.TotalSeconds:F2}s  ");
        md.AppendLine($"**Sample Count:** {report.SampleCount}  ");
        md.AppendLine();

        // Summary section
        md.AppendLine("## Summary");
        md.AppendLine();

        var successCount = report.Results.Count(r => r.IsSuccess);
        var failureCount = report.Results.Count(r => !r.IsSuccess);
        var successRate = report.Results.Count > 0
            ? (double)successCount / report.Results.Count * 100
            : 0;

        md.AppendLine($"- **Total Evaluations:** {report.Results.Count}");
        md.AppendLine($"- **Successful:** {successCount} ({successRate:F1}%)");
        md.AppendLine($"- **Failed:** {failureCount}");
        md.AppendLine($"- **Unique Metrics:** {report.Statistics.Count}");
        md.AppendLine();

        // Categorize metrics
        var retrievalMetrics = GetMetricsByCategory(report, options, "Precision", "Recall", "MRR", "F-Score", "NDCG");
        var generationMetrics = GetMetricsByCategory(report, options, "ExactMatch", "TokenF1", "BLEU", "ROUGE");
        var performanceMetrics = GetMetricsByCategory(report, options, "ResponseTime", "Throughput", "Resource");

        // Retrieval Metrics
        if (retrievalMetrics.Any())
        {
            md.AppendLine("## Retrieval Metrics");
            md.AppendLine();
            AppendMetricsTable(md, retrievalMetrics, report, options);
            md.AppendLine();
        }

        // Generation Metrics
        if (generationMetrics.Any())
        {
            md.AppendLine("## Generation Metrics");
            md.AppendLine();
            AppendMetricsTable(md, generationMetrics, report, options);
            md.AppendLine();
        }

        // Performance Metrics
        if (performanceMetrics.Any())
        {
            md.AppendLine("## Performance Metrics");
            md.AppendLine();
            AppendMetricsTable(md, performanceMetrics, report, options);
            md.AppendLine();
        }

        // Other Metrics (not categorized)
        var otherMetrics = report.Statistics.Keys
            .Except(retrievalMetrics.Concat(generationMetrics).Concat(performanceMetrics))
            .Where(m => options.MetricsToInclude == null || options.MetricsToInclude.Contains(m))
            .ToList();

        if (otherMetrics.Any())
        {
            md.AppendLine("## Other Metrics");
            md.AppendLine();
            AppendMetricsTable(md, otherMetrics, report, options);
            md.AppendLine();
        }

        // Configuration
        if (options.IncludeConfiguration && report.Configuration.Count > 0)
        {
            md.AppendLine("## Configuration");
            md.AppendLine();
            md.AppendLine("```");
            foreach (var (key, value) in report.Configuration.OrderBy(kvp => kvp.Key))
            {
                md.AppendLine($"{key}: {value}");
            }
            md.AppendLine("```");
            md.AppendLine();
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(md.ToString()));
    }

    /// <inheritdoc/>
    public Task<byte[]> ExportComparisonAsync(List<EvaluationReport> reports, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var md = new StringBuilder();

        // Title
        md.AppendLine("# Evaluation Comparison Report");
        md.AppendLine();
        md.AppendLine($"**Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        md.AppendLine($"**Number of Runs:** {reports.Count}  ");
        md.AppendLine();

        // Run details
        md.AppendLine("## Evaluated Runs");
        md.AppendLine();
        md.AppendLine("| Run # | Run ID | Started | Sample Count |");
        md.AppendLine("|-------|--------|---------|--------------|");

        for (int i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            md.AppendLine($"| {i + 1} | `{report.RunId.ToString()[..8]}...` | {report.StartedAt:yyyy-MM-dd HH:mm} | {report.SampleCount} |");
        }
        md.AppendLine();

        // Get all unique metrics
        var allMetrics = reports
            .SelectMany(r => r.Statistics.Keys)
            .Distinct()
            .ToList();

        if (options.MetricsToInclude != null)
        {
            allMetrics = allMetrics.Intersect(options.MetricsToInclude).ToList();
        }

        // Categorize metrics
        var retrievalMetrics = allMetrics.Where(m =>
            m.Contains("Precision", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Recall", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("MRR", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("F-Score", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("NDCG", StringComparison.OrdinalIgnoreCase)).ToList();

        var generationMetrics = allMetrics.Where(m =>
            m.Contains("ExactMatch", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("TokenF1", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("BLEU", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("ROUGE", StringComparison.OrdinalIgnoreCase)).ToList();

        var performanceMetrics = allMetrics.Where(m =>
            m.Contains("ResponseTime", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Throughput", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Resource", StringComparison.OrdinalIgnoreCase)).ToList();

        // Retrieval Metrics Comparison
        if (retrievalMetrics.Any())
        {
            md.AppendLine("## Retrieval Metrics Comparison");
            md.AppendLine();
            AppendComparisonTable(md, retrievalMetrics, reports, options);
            md.AppendLine();
        }

        // Generation Metrics Comparison
        if (generationMetrics.Any())
        {
            md.AppendLine("## Generation Metrics Comparison");
            md.AppendLine();
            AppendComparisonTable(md, generationMetrics, reports, options);
            md.AppendLine();
        }

        // Performance Metrics Comparison
        if (performanceMetrics.Any())
        {
            md.AppendLine("## Performance Metrics Comparison");
            md.AppendLine();
            AppendComparisonTable(md, performanceMetrics, reports, options);
            md.AppendLine();
        }

        // Legend
        md.AppendLine("## Legend");
        md.AppendLine();
        md.AppendLine("- **Δ**: Change from Run 1 baseline");
        md.AppendLine("- **%Δ**: Percent change from baseline");
        md.AppendLine("- `*`: p < 0.05 (statistically significant)");
        md.AppendLine("- `**`: p < 0.01 (highly significant)");
        md.AppendLine("- `***`: p < 0.001 (very highly significant)");
        md.AppendLine();

        return Task.FromResult(Encoding.UTF8.GetBytes(md.ToString()));
    }

    private void AppendMetricsTable(StringBuilder md, IEnumerable<string> metrics, EvaluationReport report, ExportOptions options)
    {
        // Header
        var header = "| Metric | Mean | Std Dev | Min | Max";
        var separator = "|--------|------|---------|-----|----";

        if (options.IncludePercentiles)
        {
            header += " | P50 | P95 | P99";
            separator += "|----|----|----|";
        }

        header += " | Success | Failed |";
        separator += "|---------|--------|";

        md.AppendLine(header);
        md.AppendLine(separator);

        // Rows
        foreach (var metricName in metrics)
        {
            if (!report.Statistics.TryGetValue(metricName, out var stats))
                continue;

            var row = new StringBuilder();
            row.Append($"| {metricName} ");
            row.Append($"| {stats.Mean:F4} ");
            row.Append($"| {stats.StandardDeviation:F4} ");
            row.Append($"| {stats.Min:F4} ");
            row.Append($"| {stats.Max:F4} ");

            if (options.IncludePercentiles)
            {
                var values = GetMetricValues(report, metricName);
                var p50 = PercentileCalculator.P50(values);
                var p95 = PercentileCalculator.P95(values);
                var p99 = PercentileCalculator.P99(values);
                row.Append($"| {p50:F4} ");
                row.Append($"| {p95:F4} ");
                row.Append($"| {p99:F4} ");
            }

            row.Append($"| {stats.SuccessCount} ");
            row.Append($"| {stats.FailureCount} |");

            md.AppendLine(row.ToString());
        }
    }

    private void AppendComparisonTable(StringBuilder md, IEnumerable<string> metrics, List<EvaluationReport> reports, ExportOptions options)
    {
        // Header
        var header = new StringBuilder("| Metric");
        var separator = new StringBuilder("|--------");

        for (int i = 0; i < reports.Count; i++)
        {
            header.Append($" | Run {i + 1}");
            separator.Append("|--------");

            if (i > 0)
            {
                header.Append(" | Δ | %Δ");
                separator.Append("|-----|-----|");
            }
        }

        header.Append(" |");
        separator.Append("|");

        md.AppendLine(header.ToString());
        md.AppendLine(separator.ToString());

        // Rows
        foreach (var metricName in metrics)
        {
            var row = new StringBuilder($"| {metricName}");

            MetricStatistics? baseStats = null;
            if (reports[0].Statistics.TryGetValue(metricName, out var firstStats))
            {
                baseStats = firstStats;
            }

            for (int i = 0; i < reports.Count; i++)
            {
                var report = reports[i];
                if (report.Statistics.TryGetValue(metricName, out var stats))
                {
                    row.Append($" | {stats.Mean:F4}");

                    if (i > 0 && baseStats != null)
                    {
                        var delta = stats.Mean - baseStats.Mean;
                        var percentChange = baseStats.Mean != 0 ? (delta / baseStats.Mean) * 100 : 0;

                        var significance = CalculateSignificance(baseStats, stats);
                        var deltaStr = delta >= 0 ? $"+{delta:F4}" : $"{delta:F4}";
                        var pctStr = percentChange >= 0 ? $"+{percentChange:F1}%" : $"{percentChange:F1}%";

                        row.Append($" | {deltaStr}{significance}");
                        row.Append($" | {pctStr}");
                    }
                }
                else
                {
                    row.Append(" | N/A");
                    if (i > 0)
                    {
                        row.Append(" | - | -");
                    }
                }
            }

            row.Append(" |");
            md.AppendLine(row.ToString());
        }
    }

    private List<string> GetMetricsByCategory(EvaluationReport report, ExportOptions options, params string[] patterns)
    {
        return report.Statistics.Keys
            .Where(m => patterns.Any(p => m.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .Where(m => options.MetricsToInclude == null || options.MetricsToInclude.Contains(m))
            .OrderBy(m => m)
            .ToList();
    }

    private static string CalculateSignificance(MetricStatistics baseline, MetricStatistics comparison)
    {
        // Simplified significance test using standard error
        var baselineStdErr = baseline.StandardDeviation / Math.Sqrt(baseline.SuccessCount);
        var comparisonStdErr = comparison.StandardDeviation / Math.Sqrt(comparison.SuccessCount);
        var combinedStdErr = Math.Sqrt(baselineStdErr * baselineStdErr + comparisonStdErr * comparisonStdErr);

        if (combinedStdErr == 0) return "";

        var zScore = Math.Abs(comparison.Mean - baseline.Mean) / combinedStdErr;

        // Approximate p-values
        if (zScore > 3.29) return "***"; // p < 0.001
        if (zScore > 2.58) return "**";  // p < 0.01
        if (zScore > 1.96) return "*";   // p < 0.05

        return "";
    }

    private static IEnumerable<double> GetMetricValues(EvaluationReport report, string metricName)
    {
        return report.Results
            .Where(r => r.MetricName == metricName && r.IsSuccess)
            .Select(r => r.Value);
    }
}
