using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class GenerationResponseTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesGenerationResponse()
    {
        // Arrange
        var answer = "RAG stands for Retrieval-Augmented Generation";
        var model = "gpt-4";
        var tokensUsed = 250;
        var responseTime = TimeSpan.FromSeconds(2.5);
        var sources = new List<SourceReference>
        {
            new SourceReference(Guid.NewGuid(), "Source 1", "Excerpt 1", 0.95),
            new SourceReference(Guid.NewGuid(), "Source 2", "Excerpt 2", 0.85)
        };

        // Act
        var response = new GenerationResponse(answer, model, tokensUsed, responseTime, sources);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldBe(answer);
        response.Model.ShouldBe(model);
        response.TokensUsed.ShouldBe(tokensUsed);
        response.ResponseTime.ShouldBe(responseTime);
        response.Sources.ShouldBe(sources);
        response.Sources.Count.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WithEmptyAnswer_CreatesGenerationResponse()
    {
        // Arrange & Act
        var response = new GenerationResponse("", "model", 0, TimeSpan.Zero, new List<SourceReference>());

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptySources_CreatesGenerationResponse()
    {
        // Arrange & Act
        var response = new GenerationResponse("answer", "model", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());

        // Assert
        response.Sources.ShouldNotBeNull();
        response.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var sources = new List<SourceReference>
        {
            new SourceReference(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Source", "Excerpt", 0.9)
        };
        var responseTime = TimeSpan.FromSeconds(1);
        var response1 = new GenerationResponse("answer", "model", 100, responseTime, sources);
        var response2 = new GenerationResponse("answer", "model", 100, responseTime, sources);

        // Act & Assert
        response1.ShouldBe(response2);
        (response1 == response2).ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var response1 = new GenerationResponse("answer1", "model", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());
        var response2 = new GenerationResponse("answer2", "model", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());

        // Act & Assert
        response1.ShouldNotBe(response2);
        (response1 != response2).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithMultipleSources_PreservesOrder()
    {
        // Arrange
        var source1 = new SourceReference(Guid.NewGuid(), "Source 1", "Excerpt 1", 0.95);
        var source2 = new SourceReference(Guid.NewGuid(), "Source 2", "Excerpt 2", 0.85);
        var source3 = new SourceReference(Guid.NewGuid(), "Source 3", "Excerpt 3", 0.75);
        var sources = new List<SourceReference> { source1, source2, source3 };

        // Act
        var response = new GenerationResponse("answer", "model", 100, TimeSpan.FromSeconds(1), sources);

        // Assert
        response.Sources.Count.ShouldBe(3);
        response.Sources[0].ShouldBe(source1);
        response.Sources[1].ShouldBe(source2);
        response.Sources[2].ShouldBe(source3);
    }

    [Fact]
    public void ResponseTime_WithDifferentDurations_Works()
    {
        // Arrange
        var fastResponse = new GenerationResponse("answer", "model", 50, TimeSpan.FromMilliseconds(500), new List<SourceReference>());
        var slowResponse = new GenerationResponse("answer", "model", 200, TimeSpan.FromSeconds(5), new List<SourceReference>());

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
        var response = new GenerationResponse("answer", modelName, 100, TimeSpan.FromSeconds(1), new List<SourceReference>());

        // Assert
        response.Model.ShouldBe(modelName);
    }

    [Fact]
    public void Record_Immutability_WithExpressionWorks()
    {
        // Arrange
        var original = new GenerationResponse(
            Answer: "original answer",
            Model: "gpt-4",
            TokensUsed: 100,
            ResponseTime: TimeSpan.FromSeconds(1),
            Sources: new List<SourceReference>()
        );

        // Act
        var modified = original with { Answer = "modified answer" };

        // Assert
        original.Answer.ShouldBe("original answer");
        modified.Answer.ShouldBe("modified answer");
        modified.Model.ShouldBe("gpt-4");
        modified.TokensUsed.ShouldBe(100);
    }

    [Fact]
    public void Sources_WithSourceReferences_ContainsFullMetadata()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var source = new SourceReference(sourceId, "Test Document", "This is a test excerpt", 0.92);
        var sources = new List<SourceReference> { source };

        // Act
        var response = new GenerationResponse("answer", "model", 100, TimeSpan.FromSeconds(1), sources);

        // Assert
        response.Sources[0].SourceId.ShouldBe(sourceId);
        response.Sources[0].Title.ShouldBe("Test Document");
        response.Sources[0].Excerpt.ShouldBe("This is a test excerpt");
        response.Sources[0].Score.ShouldBe(0.92);
    }
}
