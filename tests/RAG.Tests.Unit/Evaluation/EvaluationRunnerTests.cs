using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Evaluation.Configuration;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using RAG.Evaluation.Services;
using Shouldly;

namespace RAG.Tests.Unit.Evaluation;

public class EvaluationRunnerTests
{
    private readonly Mock<ILogger<EvaluationRunner>> _loggerMock;
    private readonly EvaluationOptions _options;

    public EvaluationRunnerTests()
    {
        _loggerMock = new Mock<ILogger<EvaluationRunner>>();
        _options = new EvaluationOptions { MaxParallelism = 2 };
    }

    [Fact]
    public async Task RunAsync_WithNoMetrics_ReturnsEmptyReport()
    {
        // Arrange
        var runner = CreateRunner(Enumerable.Empty<IEvaluationMetric>());
        var dataset = CreateDataset(samples: 3);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.ShouldBeEmpty();
        report.Statistics.ShouldBeEmpty();
        report.SampleCount.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WithSingleMetric_CalculatesForAllSamples()
    {
        // Arrange
        var metricMock = CreateMetricMock("TestMetric", 0.85);
        var runner = CreateRunner(new[] { metricMock.Object });
        var dataset = CreateDataset(samples: 3);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.Count.ShouldBe(3);
        report.Results.ShouldAllBe(r => r.MetricName == "TestMetric");
        report.Results.ShouldAllBe(r => r.Value == 0.85);
    }

    [Fact]
    public async Task RunAsync_WithMultipleMetrics_CalculatesAllMetricsForAllSamples()
    {
        // Arrange
        var metric1 = CreateMetricMock("Precision", 0.90);
        var metric2 = CreateMetricMock("Recall", 0.75);
        var runner = CreateRunner(new[] { metric1.Object, metric2.Object });
        var dataset = CreateDataset(samples: 2);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.Count.ShouldBe(4); // 2 samples × 2 metrics
        report.Results.Count(r => r.MetricName == "Precision").ShouldBe(2);
        report.Results.Count(r => r.MetricName == "Recall").ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_CalculatesStatistics_Correctly()
    {
        // Arrange - Use sequential options to ensure deterministic order
        var options = new EvaluationOptions { MaxParallelism = 1 };
        var callCount = 0;
        var values = new[] { 0.8, 0.9, 1.0 };

        var metricMock = new Mock<IEvaluationMetric>();
        metricMock.Setup(m => m.Name).Returns("TestMetric");
        metricMock.Setup(m => m.Description).Returns("Test");
        metricMock.Setup(m => m.CalculateAsync(It.IsAny<EvaluationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => values[Interlocked.Increment(ref callCount) - 1]);

        var runner = new EvaluationRunner(
            new[] { metricMock.Object },
            _loggerMock.Object,
            Options.Create(options));

        var dataset = CreateDataset(samples: 3);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Statistics.ContainsKey("TestMetric").ShouldBeTrue();
        var stats = report.Statistics["TestMetric"];
        stats.Mean.ShouldBe(0.9, tolerance: 0.001);
        stats.Min.ShouldBe(0.8);
        stats.Max.ShouldBe(1.0);
        stats.SuccessCount.ShouldBe(3);
        stats.FailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithMetricFailure_HandlesGracefully()
    {
        // Arrange
        var metricMock = new Mock<IEvaluationMetric>();
        metricMock.Setup(m => m.Name).Returns("FailingMetric");
        metricMock.Setup(m => m.Description).Returns("Test");
        metricMock.Setup(m => m.CalculateAsync(It.IsAny<EvaluationContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Calculation failed"));

        var runner = CreateRunner(new[] { metricMock.Object });
        var dataset = CreateDataset(samples: 2);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.Count.ShouldBe(2);
        report.Results.ShouldAllBe(r => !r.IsSuccess);
        report.Results.ShouldAllBe(r => r.ErrorMessage == "Calculation failed");

        var stats = report.Statistics["FailingMetric"];
        stats.FailureCount.ShouldBe(2);
        stats.SuccessCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithMetricsToRunFilter_OnlyRunsSpecifiedMetrics()
    {
        // Arrange
        var options = new EvaluationOptions
        {
            MaxParallelism = 2,
            MetricsToRun = new List<string> { "Precision" }
        };

        var metric1 = CreateMetricMock("Precision", 0.90);
        var metric2 = CreateMetricMock("Recall", 0.75);

        var runner = new EvaluationRunner(
            new[] { metric1.Object, metric2.Object },
            _loggerMock.Object,
            Options.Create(options));

        var dataset = CreateDataset(samples: 2);

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.ShouldAllBe(r => r.MetricName == "Precision");
        report.Results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_SkipsInvalidSamples()
    {
        // Arrange
        var metricMock = CreateMetricMock("TestMetric", 0.85);
        var runner = CreateRunner(new[] { metricMock.Object });

        var dataset = new EvaluationDataset
        {
            Name = "Test",
            Samples = new List<EvaluationContext>
            {
                new() { Query = "Valid query", Response = "Response" },
                new() { Query = "", Response = "Response" }, // Invalid - empty query
                new() { Query = "Another valid", Response = "Response" }
            }
        };

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Results.Count.ShouldBe(2); // Only 2 valid samples processed
    }

    [Fact]
    public async Task RunAsync_SetsReportMetadata_Correctly()
    {
        // Arrange
        var metricMock = CreateMetricMock("TestMetric", 0.85);
        var runner = CreateRunner(new[] { metricMock.Object });
        var dataset = new EvaluationDataset
        {
            Name = "BenchmarkDataset",
            Version = "2.0",
            Samples = new List<EvaluationContext>
            {
                new() { Query = "Test", Response = "Response" }
            }
        };

        // Act
        var report = await runner.RunAsync(dataset);

        // Assert
        report.Configuration["DatasetName"].ShouldBe("BenchmarkDataset");
        report.Configuration["DatasetVersion"].ShouldBe("2.0");
        report.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
        report.RunId.ShouldNotBe(Guid.Empty);
    }

    private EvaluationRunner CreateRunner(IEnumerable<IEvaluationMetric> metrics)
    {
        return new EvaluationRunner(
            metrics,
            _loggerMock.Object,
            Options.Create(_options));
    }

    private static Mock<IEvaluationMetric> CreateMetricMock(string name, double returnValue)
    {
        var mock = new Mock<IEvaluationMetric>();
        mock.Setup(m => m.Name).Returns(name);
        mock.Setup(m => m.Description).Returns($"{name} description");
        mock.Setup(m => m.CalculateAsync(It.IsAny<EvaluationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnValue);
        return mock;
    }

    private static EvaluationDataset CreateDataset(int samples)
    {
        var sampleList = Enumerable.Range(1, samples)
            .Select(i => new EvaluationContext
            {
                Query = $"Query {i}",
                Response = $"Response {i}",
                SampleId = $"sample-{i:D3}"
            })
            .ToList();

        return new EvaluationDataset
        {
            Name = "TestDataset",
            Samples = sampleList
        };
    }
}
