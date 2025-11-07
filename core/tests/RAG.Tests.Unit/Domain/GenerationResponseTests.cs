using RAG.Core.Domain;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Domain;

public class GenerationResponseTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesGenerationResponse()
    {
        // Arrange
        var answer = "RAG stands for Retrieval-Augmented Generation";
        var sources = new List<string> { "source1", "source2", "source3" };
        var model = "gpt-4";
        var tokensUsed = 250;
        var responseTime = TimeSpan.FromSeconds(2.5);

        // Act
        var response = new GenerationResponse(answer, sources, model, tokensUsed, responseTime);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldBe(answer);
        response.Sources.ShouldBe(sources);
        response.Model.ShouldBe(model);
        response.TokensUsed.ShouldBe(tokensUsed);
        response.ResponseTime.ShouldBe(responseTime);
    }

    [Fact]
    public void Constructor_WithEmptyAnswer_CreatesGenerationResponse()
    {
        // Arrange & Act
        var response = new GenerationResponse("", new List<string>(), "model", 0, TimeSpan.Zero);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptySources_CreatesGenerationResponse()
    {
        // Arrange & Act
        var response = new GenerationResponse("answer", new List<string>(), "model", 100, TimeSpan.FromSeconds(1));

        // Assert
        response.Sources.ShouldNotBeNull();
        response.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var sources = new List<string> { "source1" };
        var responseTime = TimeSpan.FromSeconds(1);
        var response1 = new GenerationResponse("answer", sources, "model", 100, responseTime);
        var response2 = new GenerationResponse("answer", sources, "model", 100, responseTime);

        // Act & Assert
        response1.ShouldBe(response2);
        (response1 == response2).ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var response1 = new GenerationResponse("answer1", new List<string>(), "model", 100, TimeSpan.FromSeconds(1));
        var response2 = new GenerationResponse("answer2", new List<string>(), "model", 100, TimeSpan.FromSeconds(1));

        // Act & Assert
        response1.ShouldNotBe(response2);
        (response1 != response2).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithMultipleSources_PreservesOrder()
    {
        // Arrange
        var sources = new List<string> { "source1", "source2", "source3" };

        // Act
        var response = new GenerationResponse("answer", sources, "model", 100, TimeSpan.FromSeconds(1));

        // Assert
        response.Sources.Count.ShouldBe(3);
        response.Sources[0].ShouldBe("source1");
        response.Sources[1].ShouldBe("source2");
        response.Sources[2].ShouldBe("source3");
    }

    [Fact]
    public void ResponseTime_WithDifferentDurations_Works()
    {
        // Arrange
        var fastResponse = new GenerationResponse("answer", new List<string>(), "model", 50, TimeSpan.FromMilliseconds(500));
        var slowResponse = new GenerationResponse("answer", new List<string>(), "model", 200, TimeSpan.FromSeconds(5));

        // Assert
        fastResponse.ResponseTime.ShouldBe(TimeSpan.FromMilliseconds(500));
        slowResponse.ResponseTime.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-4")]
    [InlineData("claude-3")]
    public void Constructor_WithDifferentModels_CreatesGenerationResponse(string modelName)
    {
        // Arrange & Act
        var response = new GenerationResponse("answer", new List<string>(), modelName, 100, TimeSpan.FromSeconds(1));

        // Assert
        response.Model.ShouldBe(modelName);
    }
}
