using RAG.Evaluation.Metrics.Performance;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class PerformanceMetricsTests
{
    [Fact]
    public void PercentileCalculator_P50_CalculatesMedian()
    {
        var data = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var p50 = PercentileCalculator.P50(data);

        p50.ShouldBe(5.5, tolerance: 0.001);
    }

    [Fact]
    public void PercentileCalculator_P95_CalculatesCorrectly()
    {
        var data = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        var p95 = PercentileCalculator.P95(data);

        p95.ShouldBe(95.05, tolerance: 0.1);
    }

    [Fact]
    public void PercentileCalculator_P99_CalculatesCorrectly()
    {
        var data = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        var p99 = PercentileCalculator.P99(data);

        p99.ShouldBe(99.01, tolerance: 0.1);
    }

    [Fact]
    public void PercentileCalculator_Mean_CalculatesAverage()
    {
        var data = new double[] { 10, 20, 30, 40, 50 };

        var mean = PercentileCalculator.Mean(data);

        mean.ShouldBe(30.0, tolerance: 0.001);
    }

    [Fact]
    public void PercentileCalculator_StandardDeviation_CalculatesCorrectly()
    {
        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        // Mean = 5, Variance = 4, StdDev = 2

        var stdDev = PercentileCalculator.StandardDeviation(data);

        stdDev.ShouldBe(2.0, tolerance: 0.001);
    }

    [Fact]
    public void PercentileCalculator_EmptyData_ReturnsNaN()
    {
        var data = Array.Empty<double>();

        PercentileCalculator.P50(data).ShouldBe(double.NaN);
        PercentileCalculator.Mean(data).ShouldBe(double.NaN);
        PercentileCalculator.StandardDeviation(data).ShouldBe(double.NaN);
    }

    [Fact]
    public void PercentileCalculator_SingleValue_ReturnsValue()
    {
        var data = new double[] { 42.0 };

        PercentileCalculator.P50(data).ShouldBe(42.0);
        PercentileCalculator.P95(data).ShouldBe(42.0);
        PercentileCalculator.P99(data).ShouldBe(42.0);
    }

    [Fact]
    public void PercentileCalculator_CalculateStatistics_ReturnsAllMetrics()
    {
        var data = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        var stats = PercentileCalculator.CalculateStatistics(data);

        stats.Count.ShouldBe(100);
        stats.Mean.ShouldBe(50.5, tolerance: 0.001);
        stats.Median.ShouldBe(50.5, tolerance: 0.5);
        stats.Min.ShouldBe(1.0);
        stats.Max.ShouldBe(100.0);
        stats.P50.ShouldBe(50.5, tolerance: 0.5);
        stats.P95.ShouldBeGreaterThan(90.0);
        stats.P99.ShouldBeGreaterThan(98.0);
    }

    [Fact]
    public void QueryTimingTracker_TracksQueryTiming()
    {
        var tracker = new QueryTimingTracker();
        tracker.Start();

        var timing = tracker.StartQuery("query-1");
        timing.StartRetrieval();
        Thread.Sleep(10);
        timing.StopRetrieval();
        timing.StartGeneration();
        Thread.Sleep(10);
        timing.StopGeneration();
        tracker.CompleteQuery("query-1");

        tracker.Stop();

        tracker.CompletedQueryCount.ShouldBe(1);
        timing.RetrievalMs.ShouldBeGreaterThan(0);
        timing.GenerationMs.ShouldBeGreaterThan(0);
        timing.TotalMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void QueryTimingTracker_GenerateReport_CreatesReport()
    {
        var tracker = new QueryTimingTracker();
        tracker.Start();

        for (var i = 0; i < 10; i++)
        {
            var timing = tracker.StartQuery($"query-{i}");
            Thread.Sleep(5);
            tracker.CompleteQuery($"query-{i}");
        }

        tracker.Stop();

        var report = tracker.GenerateReport();

        report.QueryCount.ShouldBe(10);
        report.Throughput.ShouldBeGreaterThan(0);
        report.TotalResponseTime.Count.ShouldBe(10);
        report.TotalResponseTime.Mean.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void QueryTimingTracker_CalculatesThroughput()
    {
        var tracker = new QueryTimingTracker();
        tracker.Start();

        for (var i = 0; i < 5; i++)
        {
            var timing = tracker.StartQuery($"query-{i}");
            tracker.CompleteQuery($"query-{i}");
        }

        // Should have positive throughput
        tracker.CurrentThroughput.ShouldBeGreaterThan(0);
    }
}
