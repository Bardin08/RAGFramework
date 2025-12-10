using RAG.Evaluation.Export;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using Xunit;
using FluentAssertions;
using System.Text;

namespace RAG.Tests.Unit.Evaluation.Export;

public class CsvResultsExporterTests
{
    private readonly CsvResultsExporter _exporter;

    public CsvResultsExporterTests()
    {
        _exporter = new CsvResultsExporter();
    }

    [Fact]
    public void Exporter_Should_Have_Correct_Properties()
    {
        // Assert
        _exporter.Format.Should().Be("CSV");
        _exporter.ContentType.Should().Be("text/csv");
        _exporter.FileExtension.Should().Be("csv");
    }

    [Fact]
    public async Task ExportAsync_Should_Generate_Valid_Csv()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { PrettyPrint = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().NotBeNullOrEmpty();
        csv.Should().Contain("# Evaluation Report");
        csv.Should().Contain("Metric,Mean,Std Dev,Min,Max");
        csv.Should().Contain("Precision@10");
        csv.Should().Contain("Recall@10");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Percentiles_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().Contain(",P50,P95,P99,");
    }

    [Fact]
    public async Task ExportAsync_Should_Exclude_Percentiles_When_Not_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludePercentiles = false };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().NotContain(",P50,");
        csv.Should().NotContain(",P95,");
        csv.Should().NotContain(",P99,");
    }

    [Fact]
    public async Task ExportAsync_Should_Include_Configuration_When_Requested()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions { IncludeConfiguration = true };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().Contain("# Configuration:");
        csv.Should().Contain("#   TopK: 10");
    }

    [Fact]
    public async Task ExportAsync_Should_Filter_Metrics_When_Specified()
    {
        // Arrange
        var report = CreateSampleReport();
        var options = new ExportOptions
        {
            MetricsToInclude = new List<string> { "Precision@10" }
        };

        // Act
        var result = await _exporter.ExportAsync(report, options);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().Contain("Precision@10");
        csv.Should().NotContain("Recall@10");
    }

    [Fact]
    public async Task ExportComparisonAsync_Should_Generate_Comparison_Csv()
    {
        // Arrange
        var reports = new List<EvaluationReport>
        {
            CreateSampleReport(runId: Guid.NewGuid()),
            CreateSampleReport(runId: Guid.NewGuid())
        };

        // Act
        var result = await _exporter.ExportComparisonAsync(reports);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().Contain("# Evaluation Comparison Report");
        csv.Should().Contain("Run_1_Mean");
        csv.Should().Contain("Run_2_Mean");
    }

    [Fact]
    public async Task ExportAsync_Should_Escape_Special_Characters_In_Values()
    {
        // Arrange
        var report = CreateSampleReport();
        var specialMetric = new KeyValuePair<string, MetricStatistics>(
            "Metric,With\"Quotes",
            new MetricStatistics("Metric,With\"Quotes", 0.5, 0.1, 0.3, 0.7, 10, 0)
        );

        var statistics = new Dictionary<string, MetricStatistics>(report.Statistics)
        {
            [specialMetric.Key] = specialMetric.Value
        };

        var reportWithSpecialChars = new EvaluationReport
        {
            RunId = report.RunId,
            StartedAt = report.StartedAt,
            CompletedAt = report.CompletedAt,
            SampleCount = report.SampleCount,
            Results = report.Results,
            Statistics = statistics,
            Configuration = report.Configuration
        };

        // Act
        var result = await _exporter.ExportAsync(reportWithSpecialChars);
        var csv = Encoding.UTF8.GetString(result);

        // Assert
        csv.Should().Contain("\"Metric,With\"\"Quotes\"");
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

            results.Add(new EvaluationResult(
                "Recall@10",
                0.75 + (i * 0.01),
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
