using RAG.Core.Domain;
using RAG.Evaluation.Metrics.Retrieval;
using RAG.Evaluation.Models;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class RetrievalMetricsTests
{
    [Fact]
    public async Task PrecisionAtK_WithRelevantDocuments_CalculatesCorrectly()
    {
        // Arrange: 3 relevant in top 5 → P@5 = 0.6
        var metric = new PrecisionAtKMetric(k: 5);
        var context = CreateContext(
            relevantIds: [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            retrievedCount: 10,
            relevantInRetrieved: 3);

        // Act
        var result = await metric.CalculateAsync(context);

        // Assert
        result.ShouldBe(0.6, tolerance: 0.001);
    }

    [Fact]
    public async Task PrecisionAtK_NoRelevantDocuments_ReturnsZero()
    {
        var metric = new PrecisionAtKMetric(k: 10);
        var context = CreateContext(
            relevantIds: [],
            retrievedCount: 10,
            relevantInRetrieved: 0);

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.0);
    }

    [Fact]
    public async Task RecallAtK_WithRelevantDocuments_CalculatesCorrectly()
    {
        // Arrange: 5 total relevant, 3 in top 10 → R@10 = 0.6
        var relevantIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var retrieved = relevantIds.Take(3)
            .Select((id, i) => new RetrievalResult(id, 0.9 - i * 0.1, "Content", $"Source {i}"))
            .Concat(Enumerable.Range(0, 7).Select(i => new RetrievalResult(Guid.NewGuid(), 0.5, "Content", $"Source {i}")))
            .ToList();

        var metric = new RecallAtKMetric(k: 10);
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "test",
            RelevantDocumentIds = relevantIds,
            RetrievedDocuments = retrieved
        };

        // Act
        var result = await metric.CalculateAsync(context);

        // Assert
        result.ShouldBe(0.6, tolerance: 0.001);
    }

    [Fact]
    public async Task MRR_FirstRelevantAtRank3_Returns0333()
    {
        // Arrange: first relevant at rank 3 → MRR = 1/3 = 0.333
        var relevantId = Guid.NewGuid();
        var retrieved = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, "Content", "Source 1"),
            new(Guid.NewGuid(), 0.8, "Content", "Source 2"),
            new(relevantId, 0.7, "Content", "Source 3"),
            new(Guid.NewGuid(), 0.6, "Content", "Source 4")
        };

        var metric = new MeanReciprocalRankMetric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "test",
            RelevantDocumentIds = [relevantId],
            RetrievedDocuments = retrieved
        };

        // Act
        var result = await metric.CalculateAsync(context);

        // Assert
        result.ShouldBe(1.0 / 3.0, tolerance: 0.001);
    }

    [Fact]
    public async Task MRR_NoRelevantFound_ReturnsZero()
    {
        var metric = new MeanReciprocalRankMetric();
        var context = CreateContext(
            relevantIds: [Guid.NewGuid()],
            retrievedCount: 5,
            relevantInRetrieved: 0);

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.0);
    }

    [Fact]
    public async Task FScore_CalculatesF1Correctly()
    {
        // Arrange: P = 0.6, R = 0.6 → F1 = 2 * 0.6 * 0.6 / (0.6 + 0.6) = 0.6
        var relevantIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var retrieved = relevantIds.Take(3)
            .Select((id, i) => new RetrievalResult(id, 0.9, "Content", $"Source {i}"))
            .Concat(Enumerable.Range(0, 2).Select(i => new RetrievalResult(Guid.NewGuid(), 0.5, "Content", $"Other {i}")))
            .ToList();

        var metric = new FScoreMetric(k: 5);
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "test",
            RelevantDocumentIds = relevantIds,
            RetrievedDocuments = retrieved
        };

        // Act
        var result = await metric.CalculateAsync(context);

        // Assert: P@5=3/5=0.6, R@5=3/5=0.6, F1=0.6
        result.ShouldBe(0.6, tolerance: 0.001);
    }

    [Fact]
    public void MetricsAggregator_MacroAverage_CalculatesCorrectly()
    {
        var values = new[] { 0.5, 0.7, 0.9 };
        var result = RetrievalMetricsAggregator.MacroAverage(values);

        result.ShouldBe(0.7, tolerance: 0.001);
    }

    [Fact]
    public void MetricsAggregator_F1Score_CalculatesCorrectly()
    {
        var f1 = RetrievalMetricsAggregator.F1Score(0.8, 0.6);

        // F1 = 2 * 0.8 * 0.6 / (0.8 + 0.6) = 0.96 / 1.4 ≈ 0.686
        f1.ShouldBe(0.686, tolerance: 0.001);
    }

    private static EvaluationContext CreateContext(
        IReadOnlyList<Guid> relevantIds,
        int retrievedCount,
        int relevantInRetrieved)
    {
        var retrieved = new List<RetrievalResult>();

        // Add relevant documents first
        for (var i = 0; i < relevantInRetrieved && i < relevantIds.Count; i++)
        {
            retrieved.Add(new RetrievalResult(relevantIds[i], 0.9 - i * 0.01, "Content", $"Source {i}"));
        }

        // Fill rest with non-relevant
        for (var i = retrieved.Count; i < retrievedCount; i++)
        {
            retrieved.Add(new RetrievalResult(Guid.NewGuid(), 0.5, "Content", $"Other {i}"));
        }

        return new EvaluationContext
        {
            Query = "test query",
            Response = "test response",
            RelevantDocumentIds = relevantIds,
            RetrievedDocuments = retrieved
        };
    }
}
