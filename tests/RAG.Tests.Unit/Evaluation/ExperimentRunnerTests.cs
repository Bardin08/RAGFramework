using Microsoft.Extensions.Logging;
using Moq;
using RAG.Evaluation.Experiments;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using RAG.Evaluation.Services;
using Shouldly;

namespace RAG.Tests.Unit.Evaluation;

public class ExperimentRunnerTests
{
    private readonly Mock<EvaluationRunner> _evaluationRunnerMock;
    private readonly Mock<StatisticalTestService> _statisticalTestMock;
    private readonly Mock<ILogger<ExperimentRunner>> _loggerMock;
    private readonly ExperimentRunner _experimentRunner;

    public ExperimentRunnerTests()
    {
        var evalLoggerMock = new Mock<ILogger<EvaluationRunner>>();
        var statsLoggerMock = new Mock<ILogger<StatisticalTestService>>();
        _loggerMock = new Mock<ILogger<ExperimentRunner>>();

        _evaluationRunnerMock = new Mock<EvaluationRunner>(
            new Mock<IEnumerable<IEvaluationMetric>>().Object,
            evalLoggerMock.Object,
            Microsoft.Extensions.Options.Options.Create(new RAG.Evaluation.Configuration.EvaluationOptions()));

        _statisticalTestMock = new Mock<StatisticalTestService>(statsLoggerMock.Object);

        _experimentRunner = new ExperimentRunner(
            _evaluationRunnerMock.Object,
            _statisticalTestMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RunExperimentAsync_WithInvalidExperiment_ThrowsException()
    {
        // Arrange
        var invalidExperiment = new ConfigurationExperiment
        {
            Name = "",
            Dataset = "test",
            Variants = new List<ExperimentVariant>(),
            Metrics = new List<string>()
        };
        var dataset = CreateDataset();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() =>
            _experimentRunner.RunExperimentAsync(invalidExperiment, dataset));
    }

    [Fact]
    public async Task RunExperimentAsync_WithTwoVariants_RunsBothVariants()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockReport());

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        results.VariantResults.Count.ShouldBe(2);
        results.VariantResults.ContainsKey("BM25-Only").ShouldBeTrue();
        results.VariantResults.ContainsKey("Hybrid").ShouldBeTrue();
        _evaluationRunnerMock.Verify(
            x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunExperimentAsync_CalculatesCompositeScores()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockReport());

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        foreach (var variant in results.VariantResults.Values)
        {
            variant.CompositeScore.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public async Task RunExperimentAsync_SelectsWinner()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        var report1 = CreateMockReport(precision: 0.7, recall: 0.6, mrr: 0.75, f1: 0.65);
        var report2 = CreateMockReport(precision: 0.8, recall: 0.75, mrr: 0.85, f1: 0.77);

        var callCount = 0;
        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? report1 : report2);

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        results.WinnerVariantName.ShouldNotBeNull();
        var winner = results.VariantResults[results.WinnerVariantName];
        winner.IsWinner.ShouldBeTrue();

        // Hybrid (second variant) should win with better scores
        results.WinnerVariantName.ShouldBe("Hybrid");
    }

    [Fact]
    public async Task RunExperimentAsync_PerformsStatisticalComparisons()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockReport());

        _statisticalTestMock
            .Setup(x => x.PairedTTest(It.IsAny<double[]>(), It.IsAny<double[]>()))
            .Returns((2.5, 0.03));

        _statisticalTestMock
            .Setup(x => x.CalculateCohenD(It.IsAny<double[]>(), It.IsAny<double[]>()))
            .Returns(0.8);

        _statisticalTestMock
            .Setup(x => x.BonferroniCorrection(It.IsAny<double>(), It.IsAny<int>()))
            .Returns<double, int>((p, n) => Math.Min(p * n, 1.0));

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        results.Comparisons.ShouldNotBeEmpty();
        // Should compare on all metrics between 2 variants
        results.Comparisons.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RunExperimentAsync_AppliesBonferroniCorrection()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockReport());

        _statisticalTestMock
            .Setup(x => x.PairedTTest(It.IsAny<double[]>(), It.IsAny<double[]>()))
            .Returns((2.5, 0.01));

        _statisticalTestMock
            .Setup(x => x.BonferroniCorrection(It.IsAny<double>(), It.IsAny<int>()))
            .Returns<double, int>((p, n) => p * n);

        _statisticalTestMock
            .Setup(x => x.CalculateCohenD(It.IsAny<double[]>(), It.IsAny<double[]>()))
            .Returns(0.5);

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        var comparisonCount = results.Comparisons.Count;
        if (comparisonCount > 0)
        {
            _statisticalTestMock.Verify(
                x => x.BonferroniCorrection(It.IsAny<double>(), comparisonCount),
                Times.AtLeastOnce());
        }
    }

    [Fact]
    public async Task RunExperimentAsync_RecordsTimestamps()
    {
        // Arrange
        var experiment = CreateValidExperiment();
        var dataset = CreateDataset();

        _evaluationRunnerMock
            .Setup(x => x.RunAsync(It.IsAny<EvaluationDataset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockReport());

        var startTime = DateTimeOffset.UtcNow;

        // Act
        var results = await _experimentRunner.RunExperimentAsync(experiment, dataset);

        // Assert
        results.StartedAt.ShouldBeGreaterThanOrEqualTo(startTime);
        results.CompletedAt.ShouldBeGreaterThanOrEqualTo(results.StartedAt);
        results.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    private ConfigurationExperiment CreateValidExperiment()
    {
        return new ConfigurationExperiment
        {
            Name = "Test Experiment",
            Dataset = "natural-questions",
            BaseConfiguration = new Dictionary<string, object>
            {
                ["topK"] = 10
            },
            Variants = new List<ExperimentVariant>
            {
                new()
                {
                    Name = "BM25-Only",
                    Parameters = new VariantParameters { RetrievalStrategy = "bm25" }
                },
                new()
                {
                    Name = "Hybrid",
                    Parameters = new VariantParameters { RetrievalStrategy = "hybrid", HybridAlpha = 0.7 }
                }
            },
            Metrics = new List<string> { "precision@10", "recall@10", "mrr", "f1" },
            PrimaryMetric = "f1"
        };
    }

    private EvaluationDataset CreateDataset()
    {
        return new EvaluationDataset
        {
            Name = "test-dataset",
            Version = "1.0",
            Samples = new List<EvaluationContext>
            {
                new() { Query = "test query 1", Response = "response 1" },
                new() { Query = "test query 2", Response = "response 2" },
                new() { Query = "test query 3", Response = "response 3" }
            }
        };
    }

    private EvaluationReport CreateMockReport(
        double precision = 0.75,
        double recall = 0.70,
        double mrr = 0.80,
        double f1 = 0.72)
    {
        var results = new List<EvaluationResult>
        {
            new("precision@10", precision, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("precision@10", precision + 0.05, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("recall@10", recall, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("recall@10", recall + 0.05, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("mrr", mrr, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("mrr", mrr + 0.03, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("f1", f1, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            new("f1", f1 + 0.04, DateTimeOffset.UtcNow, new Dictionary<string, object>())
        };

        var statistics = new Dictionary<string, MetricStatistics>
        {
            ["precision@10"] = new MetricStatistics("precision@10", precision, 0.05, precision - 0.05, precision + 0.05, 2, 0),
            ["recall@10"] = new MetricStatistics("recall@10", recall, 0.05, recall - 0.05, recall + 0.05, 2, 0),
            ["mrr"] = new MetricStatistics("mrr", mrr, 0.03, mrr - 0.03, mrr + 0.03, 2, 0),
            ["f1"] = new MetricStatistics("f1", f1, 0.04, f1 - 0.04, f1 + 0.04, 2, 0)
        };

        return new EvaluationReport
        {
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            SampleCount = 3,
            Results = results,
            Statistics = statistics,
            Configuration = new Dictionary<string, object>()
        };
    }
}
