using RAG.Evaluation.Export;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using Xunit;
using FluentAssertions;
using System.Text;

namespace RAG.Tests.Unit.Evaluation.Export;

public class MarkdownResultsExporterTests
{
    private readonly MarkdownResultsExporter _exporter;

    public MarkdownResultsExporterTests()
    {
        _exporter = new MarkdownResultsExporter();
    }

    [Fact]
    public void Exporter_Should_Have_Correct_Properties()
    {
        // Assert
        _exporter.Format.Should().Be("Markdown");
        _exporter.ContentType.Should().Be("text/markdown");
        _exporter.FileExtension.Should().Be("md");
    }

    [Fact]
    public async Task ExportAsync_Should_Generate_Valid_Markdown()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var result = await _exporter.ExportAsync(report);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().StartWith("# ");
        markdown.Should().Contain("## Summary");
        markdown.Should().Contain("**Run ID:**");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_DatasetName_In_Title()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { DatasetName = "Natural Questions" };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("# Natural Questions");
    }

    [Fact]
    public async Task ExportAsync_Should_Categorize_Metrics()
    {
        // Arrange
        var report = CreateSampleReportWithAllMetricTypes();

        // Act
        var result = await _exporter.ExportAsync(report);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("## Retrieval Metrics");
        markdown.Should().Contain("## Generation Metrics");
        markdown.Should().Contain("## Performance Metrics");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Summary_Section()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var result = await _exporter.ExportAsync(report);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("## Summary");
        markdown.Should().Contain("**Total Evaluations:**");
        markdown.Should().Contain("**Successful:**");
        markdown.Should().Contain("**Failed:**");
        markdown.Should().Contain("**Unique Metrics:**");
    }

    [Fact]
    public async Task ExportAsync_Should_Generate_Metrics_Tables()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var result = await _exporter.ExportAsync(report);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("| Metric | Mean | Std Dev | Min | Max");
        markdown.Should().Contain("|--------|------|---------|-----|----");
        markdown.Should().Contain("| Precision@10 |");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Percentiles_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("| P50 | P95 | P99 |");
    }

    [Fact]
    public async Task ExportAsync_Should_Exclude_Percentiles_When_Not_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = false };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().NotContain("| P50 |");
        markdown.Should().NotContain("| P95 |");
        markdown.Should().NotContain("| P99 |");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Configuration_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludeConfiguration = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("## Configuration");
        markdown.Should().Contain("TopK: 10");
        markdown.Should().Contain("Model: test-model");
    }

    [Fact]
    public async Task ExportAsync_Should_Exclude_Configuration_When_Not_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludeConfiguration = false };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().NotContain("## Configuration");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Generate_Comparison_Markdown()
    {
        // Arrange
        var reports = new List<EvaluationReport>
        {
            CreateSampleReport(runId: Guid.NewGuid()),
            CreateSampleReport(runId: Guid.NewGuid())
        };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("# Evaluation Comparison Report");
        markdown.Should().Contain("## Evaluated Runs");
        markdown.Should().Contain("| Run # | Run ID | Started | Sample Count |");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Include_Comparison_Tables()
    {
        // Arrange
        var reports = new List<EvaluationReport>
        {
            CreateSampleReport(runId: Guid.NewGuid()),
            CreateSampleReport(runId: Guid.NewGuid())
        };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert - Table contains Run columns and delta columns
        markdown.Should().Contain("| Run 1 |");
        markdown.Should().Contain("| Run 2 |");
        markdown.Should().Contain("| Δ |");
        markdown.Should().Contain("| %Δ |");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Include_Legend()
    {
        // Arrange
        var reports = new List<EvaluationReport>
        {
            CreateSampleReport(runId: Guid.NewGuid()),
            CreateSampleReport(runId: Guid.NewGuid())
        };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        markdown.Should().Contain("## Legend");
        markdown.Should().Contain("`*`: p < 0.05");
        markdown.Should().Contain("`**`: p < 0.01");
        markdown.Should().Contain("`***`: p < 0.001");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Show_Significance_Indicators()
    {
        // Arrange
        var report1 = CreateSampleReport(runId: Guid.NewGuid());

        // Create report2 with significantly different values
        var stats2 = new Dictionary<string, MetricStatistics>
        {
            ["Precision@10"] = new MetricStatistics("Precision@10", 0.95, 0.05, 0.85, 0.99, 100, 0),
            ["Recall@10"] = new MetricStatistics("Recall@10", 0.85, 0.08, 0.70, 0.98, 100, 0)
        };

        var report2 = new EvaluationReport
        {
            RunId = Guid.NewGuid(),
            StartedAt = report1.StartedAt,
            CompletedAt = report1.CompletedAt,
            SampleCount = 100,
            Results = report1.Results,
            Statistics = stats2,
            Configuration = report1.Configuration
        };

        var reports = new List<EvaluationReport> { report1, report2 };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var markdown = Encoding.UTF8.GetString(result);

        // Assert
        // Should contain delta indicators
        markdown.Should().Match(m => m.Contains("+") || m.Contains("-"));
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

    private static EvaluationReport CreateSampleReportWithAllMetricTypes()
    {
        var statistics = new Dictionary<string, MetricStatistics>
        {
            // Retrieval metrics
            ["Precision@10"] = new MetricStatistics("Precision@10", 0.85, 0.12, 0.60, 0.98, 100, 0),
            ["Recall@10"] = new MetricStatistics("Recall@10", 0.75, 0.15, 0.45, 0.95, 100, 0),
            ["MRR"] = new MetricStatistics("MRR", 0.82, 0.10, 0.55, 0.99, 100, 0),

            // Generation metrics
            ["ExactMatch"] = new MetricStatistics("ExactMatch", 0.45, 0.20, 0.10, 0.80, 100, 0),
            ["TokenF1"] = new MetricStatistics("TokenF1", 0.72, 0.15, 0.40, 0.92, 100, 0),
            ["BLEU"] = new MetricStatistics("BLEU", 0.68, 0.18, 0.35, 0.90, 100, 0),

            // Performance metrics
            ["ResponseTime"] = new MetricStatistics("ResponseTime", 125.5, 25.3, 80.0, 200.0, 100, 0),
            ["Throughput"] = new MetricStatistics("Throughput", 8.5, 1.2, 6.0, 11.0, 100, 0)
        };

        return new EvaluationReport
        {
            RunId = Guid.NewGuid(),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow,
            SampleCount = 10,
            Results = [],
            Statistics = statistics,
            Configuration = new Dictionary<string, object>()
        };
    }
}
