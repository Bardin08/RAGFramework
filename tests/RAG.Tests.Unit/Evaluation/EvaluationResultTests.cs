using RAG.Evaluation.Models;
using Shouldly;

namespace RAG.Tests.Unit.Evaluation;

public class EvaluationResultTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesResult()
    {
        // Arrange
        var metricName = "Precision@5";
        var value = 0.85;
        var timestamp = DateTimeOffset.UtcNow;
        var config = new Dictionary<string, object> { ["k"] = 5 };

        // Act
        var result = new EvaluationResult(metricName, value, timestamp, config);

        // Assert
        result.MetricName.ShouldBe(metricName);
        result.Value.ShouldBe(value);
        result.Timestamp.ShouldBe(timestamp);
        result.Configuration.ShouldBe(config);
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["SampleId"] = "sample-001",
            ["DurationMs"] = 150
        };

        // Act
        var result = new EvaluationResult("NDCG", 0.72, DateTimeOffset.UtcNow, new Dictionary<string, object>())
        {
            Metadata = metadata
        };

        // Assert
        result.Metadata.ContainsKey("SampleId").ShouldBeTrue();
        result.Metadata["SampleId"].ShouldBe("sample-001");
        result.Metadata["DurationMs"].ShouldBe(150);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Arrange
        var metricName = "AnswerRelevance";
        var errorMessage = "LLM provider unavailable";

        // Act
        var result = EvaluationResult.Failure(metricName, errorMessage);

        // Assert
        result.MetricName.ShouldBe(metricName);
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(errorMessage);
        double.IsNaN(result.Value).ShouldBeTrue();
    }

    [Fact]
    public void Record_PreservesImmutability()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var config = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = new EvaluationResult("Metric", 0.5, timestamp, config);
        var withModifiedValue = result with { Value = 0.75 };

        // Assert - Original is unchanged
        result.Value.ShouldBe(0.5);
        withModifiedValue.Value.ShouldBe(0.75);
        withModifiedValue.MetricName.ShouldBe(result.MetricName);
    }
}
