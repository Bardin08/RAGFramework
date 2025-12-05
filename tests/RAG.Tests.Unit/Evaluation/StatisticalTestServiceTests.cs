using Microsoft.Extensions.Logging;
using Moq;
using RAG.Evaluation.Services;
using Shouldly;

namespace RAG.Tests.Unit.Evaluation;

public class StatisticalTestServiceTests
{
    private readonly Mock<ILogger<StatisticalTestService>> _loggerMock;
    private readonly StatisticalTestService _service;

    public StatisticalTestServiceTests()
    {
        _loggerMock = new Mock<ILogger<StatisticalTestService>>();
        _service = new StatisticalTestService(_loggerMock.Object);
    }

    [Fact]
    public void PairedTTest_WithIdenticalSamples_ReturnsZeroTStatistic()
    {
        // Arrange
        var sample1 = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var sample2 = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        var (tStatistic, pValue) = _service.PairedTTest(sample1, sample2);

        // Assert
        tStatistic.ShouldBe(0.0, tolerance: 0.001);
        pValue.ShouldBeGreaterThan(0.05); // Not significant
    }

    [Fact]
    public void PairedTTest_WithDifferentSamples_ReturnsNonZeroTStatistic()
    {
        // Arrange
        var sample1 = new[] { 10.0, 12.0, 14.0, 16.0, 18.0 };
        var sample2 = new[] { 5.0, 6.0, 7.0, 8.0, 9.0 };

        // Act
        var (tStatistic, pValue) = _service.PairedTTest(sample1, sample2);

        // Assert
        tStatistic.ShouldNotBe(0.0);
        Math.Abs(tStatistic).ShouldBeGreaterThan(1.0); // Should have substantial t-value
        pValue.ShouldBeLessThan(0.05); // Should be significant
    }

    [Fact]
    public void PairedTTest_WithUnequalLengths_ThrowsException()
    {
        // Arrange
        var sample1 = new[] { 1.0, 2.0, 3.0 };
        var sample2 = new[] { 1.0, 2.0 };

        // Act & Assert
        Should.Throw<ArgumentException>(() => _service.PairedTTest(sample1, sample2));
    }

    [Fact]
    public void PairedTTest_WithSmallSample_ReturnsValidResults()
    {
        // Arrange
        var sample1 = new[] { 5.0, 7.0 };
        var sample2 = new[] { 4.0, 6.0 };

        // Act
        var (tStatistic, pValue) = _service.PairedTTest(sample1, sample2);

        // Assert
        tStatistic.ShouldNotBe(double.NaN);
        pValue.ShouldBeInRange(0.0, 1.0);
    }

    [Fact]
    public void BonferroniCorrection_WithSingleComparison_ReturnsOriginalPValue()
    {
        // Arrange
        var originalPValue = 0.03;
        var numberOfComparisons = 1;

        // Act
        var adjustedPValue = _service.BonferroniCorrection(originalPValue, numberOfComparisons);

        // Assert
        adjustedPValue.ShouldBe(originalPValue);
    }

    [Fact]
    public void BonferroniCorrection_WithMultipleComparisons_IncreasesThreshold()
    {
        // Arrange
        var originalPValue = 0.03;
        var numberOfComparisons = 5;

        // Act
        var adjustedPValue = _service.BonferroniCorrection(originalPValue, numberOfComparisons);

        // Assert
        adjustedPValue.ShouldBe(0.15); // 0.03 * 5
        adjustedPValue.ShouldBeGreaterThan(originalPValue);
    }

    [Fact]
    public void BonferroniCorrection_WithLargePValue_CapsAtOne()
    {
        // Arrange
        var originalPValue = 0.3;
        var numberOfComparisons = 10;

        // Act
        var adjustedPValue = _service.BonferroniCorrection(originalPValue, numberOfComparisons);

        // Assert
        adjustedPValue.ShouldBe(1.0); // Capped at 1.0
    }

    [Fact]
    public void BonferroniCorrection_WithZeroComparisons_ThrowsException()
    {
        // Arrange
        var pValue = 0.05;
        var numberOfComparisons = 0;

        // Act & Assert
        Should.Throw<ArgumentException>(() => _service.BonferroniCorrection(pValue, numberOfComparisons));
    }

    [Fact]
    public void CalculateCohenD_WithNoEffect_ReturnsZero()
    {
        // Arrange
        var sample1 = new[] { 10.0, 12.0, 14.0, 16.0, 18.0 };
        var sample2 = new[] { 10.0, 12.0, 14.0, 16.0, 18.0 };

        // Act
        var effectSize = _service.CalculateCohenD(sample1, sample2);

        // Assert
        effectSize.ShouldBe(0.0, tolerance: 0.001);
    }

    [Fact]
    public void CalculateCohenD_WithLargeEffect_ReturnsLargeValue()
    {
        // Arrange
        var sample1 = new[] { 20.0, 22.0, 24.0, 26.0, 28.0 };
        var sample2 = new[] { 5.0, 7.0, 9.0, 11.0, 13.0 };

        // Act
        var effectSize = _service.CalculateCohenD(sample1, sample2);

        // Assert
        Math.Abs(effectSize).ShouldBeGreaterThan(1.0); // Large effect
    }

    [Fact]
    public void CalculateCohenD_WithUnequalLengths_ThrowsException()
    {
        // Arrange
        var sample1 = new[] { 1.0, 2.0, 3.0 };
        var sample2 = new[] { 1.0, 2.0 };

        // Act & Assert
        Should.Throw<ArgumentException>(() => _service.CalculateCohenD(sample1, sample2));
    }

    [Fact]
    public void PairedTTest_WithRealWorldData_CalculatesCorrectly()
    {
        // Arrange - Simulating precision scores from two retrieval strategies
        var bm25Scores = new[] { 0.75, 0.80, 0.82, 0.78, 0.76, 0.81, 0.79, 0.77, 0.83, 0.80 };
        var hybridScores = new[] { 0.85, 0.88, 0.90, 0.86, 0.84, 0.89, 0.87, 0.85, 0.91, 0.88 };

        // Act
        var (tStatistic, pValue) = _service.PairedTTest(hybridScores, bm25Scores);

        // Assert
        tStatistic.ShouldBeGreaterThan(0); // Hybrid should be better
        pValue.ShouldBeLessThan(0.05); // Should be statistically significant
    }
}
