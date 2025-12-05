namespace RAG.Evaluation.Export;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Metrics.Performance;
using RAG.Evaluation.Models;

/// <summary>
/// Exports evaluation results to JSON format.
/// </summary>
public class JsonResultsExporter : IResultsExporter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public string Format => "JSON";
    public string ContentType => "application/json";
    public string FileExtension => "json";

    /// <inheritdoc/>
    public Task<byte[]> ExportAsync(EvaluationReport report, ExportOptions? options = null)
    {
        options ??= new ExportOptions();

        var exportData = new
        {
            metadata = new
            {
                jobId = report.RunId,
                startedAt = report.StartedAt,
                completedAt = report.CompletedAt,
                duration = report.Duration.TotalSeconds,
                sampleCount = report.SampleCount,
                dataset = options.DatasetName,
                configuration = options.IncludeConfiguration ? report.Configuration : null
            },
            aggregated = BuildAggregatedMetrics(report, options),
            perQuery = options.IncludePerQueryBreakdown ? BuildPerQueryMetrics(report, options) : null
        };

        var serializerOptions = options.PrettyPrint ? DefaultOptions : CompactOptions;
        var json = JsonSerializer.Serialize(exportData, serializerOptions);
        return Task.FromResult(Encoding.UTF8.GetBytes(json));
    }

    /// <inheritdoc/>
    public Task<byte[]> ExportComparisonAsync(List<EvaluationReport> reports, ExportOptions? options = null)
    {
        options ??= new ExportOptions();

        var exportData = new
        {
            metadata = new
            {
                comparisonId = Guid.NewGuid(),
                generatedAt = DateTimeOffset.UtcNow,
                reportCount = reports.Count,
                reports = reports.Select(r => new
                {
                    runId = r.RunId,
                    startedAt = r.StartedAt,
                    completedAt = r.CompletedAt,
                    sampleCount = r.SampleCount
                })
            },
            comparison = BuildComparisonMetrics(reports, options),
            individual = reports.Select(r => new
            {
                runId = r.RunId,
                metrics = BuildAggregatedMetrics(r, options)
            })
        };

        var serializerOptions = options.PrettyPrint ? DefaultOptions : CompactOptions;
        var json = JsonSerializer.Serialize(exportData, serializerOptions);
        return Task.FromResult(Encoding.UTF8.GetBytes(json));
    }

    private Dictionary<string, object> BuildAggregatedMetrics(EvaluationReport report, ExportOptions options)
    {
        var metrics = options.MetricsToInclude != null
            ? report.Statistics.Where(kvp => options.MetricsToInclude.Contains(kvp.Key))
            : report.Statistics;

        var result = new Dictionary<string, object>();

        foreach (var (metricName, stats) in metrics)
        {
            var metricData = new Dictionary<string, object>
            {
                ["mean"] = Math.Round(stats.Mean, 4),
                ["standardDeviation"] = Math.Round(stats.StandardDeviation, 4),
                ["min"] = Math.Round(stats.Min, 4),
                ["max"] = Math.Round(stats.Max, 4),
                ["successCount"] = stats.SuccessCount,
                ["failureCount"] = stats.FailureCount
            };

            if (options.IncludePercentiles)
            {
                var values = GetMetricValues(report, metricName);
                metricData["p50"] = Math.Round(PercentileCalculator.P50(values), 4);
                metricData["p95"] = Math.Round(PercentileCalculator.P95(values), 4);
                metricData["p99"] = Math.Round(PercentileCalculator.P99(values), 4);
            }

            result[metricName] = metricData;
        }

        return result;
    }

    private List<object> BuildPerQueryMetrics(EvaluationReport report, ExportOptions options)
    {
        var metricNames = options.MetricsToInclude != null
            ? report.Results.Select(r => r.MetricName).Distinct().Intersect(options.MetricsToInclude).ToList()
            : report.Results.Select(r => r.MetricName).Distinct().ToList();

        var groupedResults = report.Results
            .Where(r => metricNames.Contains(r.MetricName))
            .GroupBy(r => GetQueryIndex(r))
            .OrderBy(g => g.Key);

        var perQueryData = new List<object>();

        foreach (var group in groupedResults)
        {
            var queryData = new Dictionary<string, object>
            {
                ["queryIndex"] = group.Key
            };

            var metrics = new Dictionary<string, object>();
            foreach (var result in group)
            {
                if (result.IsSuccess)
                {
                    metrics[result.MetricName] = Math.Round(result.Value, 4);
                }
                else
                {
                    metrics[result.MetricName] = new
                    {
                        error = result.ErrorMessage,
                        value = (double?)null
                    };
                }
            }

            queryData["metrics"] = metrics;

            // Include query metadata if available
            var firstResult = group.First();
            if (firstResult.Metadata.TryGetValue("Query", out var query))
            {
                queryData["query"] = query;
            }

            perQueryData.Add(queryData);
        }

        return perQueryData;
    }

    private Dictionary<string, object> BuildComparisonMetrics(List<EvaluationReport> reports, ExportOptions options)
    {
        var allMetrics = reports
            .SelectMany(r => r.Statistics.Keys)
            .Distinct()
            .ToList();

        if (options.MetricsToInclude != null)
        {
            allMetrics = allMetrics.Intersect(options.MetricsToInclude).ToList();
        }

        var comparison = new Dictionary<string, object>();

        foreach (var metricName in allMetrics)
        {
            var metricComparison = new List<object>();

            for (int i = 0; i < reports.Count; i++)
            {
                var report = reports[i];
                if (report.Statistics.TryGetValue(metricName, out var stats))
                {
                    var data = new Dictionary<string, object>
                    {
                        ["runId"] = report.RunId,
                        ["runIndex"] = i,
                        ["mean"] = Math.Round(stats.Mean, 4),
                        ["standardDeviation"] = Math.Round(stats.StandardDeviation, 4)
                    };

                    if (options.IncludePercentiles)
                    {
                        var values = GetMetricValues(report, metricName);
                        data["p50"] = Math.Round(PercentileCalculator.P50(values), 4);
                        data["p95"] = Math.Round(PercentileCalculator.P95(values), 4);
                        data["p99"] = Math.Round(PercentileCalculator.P99(values), 4);
                    }

                    // Calculate delta from first report
                    if (i > 0 && reports[0].Statistics.TryGetValue(metricName, out var baseStats))
                    {
                        var delta = stats.Mean - baseStats.Mean;
                        var percentChange = baseStats.Mean != 0 ? (delta / baseStats.Mean) * 100 : double.NaN;

                        data["delta"] = Math.Round(delta, 4);
                        data["percentChange"] = Math.Round(percentChange, 2);
                    }

                    metricComparison.Add(data);
                }
            }

            comparison[metricName] = metricComparison;
        }

        return comparison;
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
