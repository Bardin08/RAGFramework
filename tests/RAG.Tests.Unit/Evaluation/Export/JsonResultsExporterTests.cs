using RAG.Evaluation.Export;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using Xunit;
using FluentAssertions;
using System.Text;
using System.Text.Json;

namespace RAG.Tests.Unit.Evaluation.Export;

public class JsonResultsExporterTests
{
    private readonly JsonResultsExporter _exporter;

    public JsonResultsExporterTests()
    {
        _exporter = new JsonResultsExporter();
    }

    [Fact]
    public void Exporter_Should_Have_Correct_Properties()
    {
        // Assert
        _exporter.Format.Should().Be("JSON");
        _exporter.ContentType.Should().Be("application/json");
        _exporter.FileExtension.Should().Be("json");
    }

    [Fact]
    public async Task ExportAsync_Should_Generate_Valid_Json()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var result = await _exporter.ExportAsync(report);
        var json = Encoding.UTF8.GetString(result);

        // Assert
        json.Should().NotBeNullOrEmpty();

        // Verify it's valid JSON
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("metadata").GetProperty("jobId").GetGuid().Should().Be(report.RunId);
        doc.RootElement.GetProperty("aggregated").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Metadata()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { DatasetName = "Test Dataset" };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var metadata = doc.RootElement.GetProperty("metadata");
        metadata.GetProperty("dataset").GetString().Should().Be("Test Dataset");
        metadata.GetProperty("sampleCount").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Aggregated_Metrics()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var result = await _exporter.ExportAsync(report);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var aggregated = doc.RootElement.GetProperty("aggregated");
        aggregated.GetProperty("Precision@10").GetProperty("mean").GetDouble().Should().BeApproximately(0.85, 0.01);
        aggregated.GetProperty("Precision@10").GetProperty("standardDeviation").GetDouble().Should().BeApproximately(0.12, 0.01);
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Percentiles_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var precision = doc.RootElement.GetProperty("aggregated").GetProperty("Precision@10");
        precision.TryGetProperty("p50", out _).Should().BeTrue();
        precision.TryGetProperty("p95", out _).Should().BeTrue();
        precision.TryGetProperty("p99", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_Should_Exclude_Percentiles_When_Not_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = false };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var precision = doc.RootElement.GetProperty("aggregated").GetProperty("Precision@10");
        precision.TryGetProperty("p50", out _).Should().BeFalse();
        precision.TryGetProperty("p95", out _).Should().BeFalse();
        precision.TryGetProperty("p99", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_Should_Include_PerQuery_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePerQueryBreakdown = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.TryGetProperty("perQuery", out var perQuery).Should().BeTrue();
        perQuery.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportAsync_Should_Exclude_PerQuery_When_Not_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePerQueryBreakdown = false };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var perQuery = doc.RootElement.GetProperty("perQuery");
        perQuery.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ExportAsync_Should_Use_PrettyPrint_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var optionsPretty = new ExportOptions { PrettyPrint = true };
        var optionsCompact = new ExportOptions { PrettyPrint = false };

        // Act
        var prettyResult = await _exporter.ExportAsync(report, optionsPretty);
        var compactResult = await _exporter.ExportAsync(report, optionsCompact);

        // Assert
        prettyResult.Length.Should().BeGreaterThan(compactResult.Length);
        Encoding.UTF8.GetString(prettyResult).Should().Contain("\n");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Generate_Comparison_Json()
    {
        // Arrange
        var reports = new List<EvaluationReport>
        {
            CreateSampleReport(runId: Guid.NewGuid()),
            CreateSampleReport(runId: Guid.NewGuid())
        };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.GetProperty("metadata").GetProperty("reportCount").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("comparison").EnumerateObject().Should().NotBeEmpty();
        doc.RootElement.GetProperty("individual").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Calculate_Deltas()
    {
        // Arrange
        var report1 = CreateSampleReport(runId: Guid.NewGuid());
        var report2 = CreateSampleReport(runId: Guid.NewGuid());

        // Modify report2 to have different values
        var stats2 = new Dictionary<string, MetricStatistics>(report2.Statistics);
        stats2["Precision@10"] = new MetricStatistics("Precision@10", 0.90, 0.10, 0.70, 0.99, 100, 0);

        var modifiedReport2 = new EvaluationReport
        {
            RunId = report2.RunId,
            StartedAt = report2.StartedAt,
            CompletedAt = report2.CompletedAt,
            SampleCount = report2.SampleCount,
            Results = report2.Results,
            Statistics = stats2,
            Configuration = report2.Configuration
        };

        var reports = new List<EvaluationReport> { report1, modifiedReport2 };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var json = Encoding.UTF8.GetString(result);
        var doc = JsonDocument.Parse(json);

        // Assert
        var comparison = doc.RootElement.GetProperty("comparison").GetProperty("Precision@10");
        var run2Data = comparison[1];
        run2Data.GetProperty("delta").GetDouble().Should().BeApproximately(0.05, 0.01);
        run2Data.TryGetProperty("percentChange", out _).Should().BeTrue();
    }

    private static EvaluationReport CreateSampleReport(Guid? runId = null)
    {
        var id = runId ?? Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var completedAt = DateTimeOffset.UtcNow;

        var statistics = new Dictionary<string, MetricStatistics>
        {
            ["Precision@10"] = new MetricStatistics("Precision@10", 0.85, 0.12, 0.60, 0.98, 100, 0),
            ["Recall@10"] = new MetricStatistics("Recall@10", 0.75, 0.15, 0.45, 0.95, 100, 0),
            ["MRR"] = new MetricStatistics("MRR", 0.82, 0.10, 0.55, 0.99, 100, 0)
        };

        var results = new List<EvaluationResult>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(new EvaluationResult(
                "Precision@10",
                0.85 + (i * 0.01),
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>())
            {
                Metadata = new Dictionary<string, object> { ["QueryIndex"] = i }
            });
        }

        return new EvaluationReport
        {
            RunId = id,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            SampleCount = 10,
            Results = results,
            Statistics = statistics,
            Configuration = new Dictionary<string, object>
            {
                ["TopK"] = 10,
                ["Model"] = "test-model"
            }
        };
    }
}
