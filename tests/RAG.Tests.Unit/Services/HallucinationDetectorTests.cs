using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Configuration;
using RAG.Application.Models;
using RAG.Application.Services;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Services;

public class HallucinationDetectorTests
{
    private readonly Mock<ILogger<HallucinationDetector>> _loggerMock;
    private readonly HallucinationDetectionSettings _defaultSettings;

    public HallucinationDetectorTests()
    {
        _loggerMock = new Mock<ILogger<HallucinationDetector>>();
        _defaultSettings = new HallucinationDetectionSettings
        {
            EnableContextGrounding = true,
            EnableSelfConsistency = false,
            EnableLLMJudge = false,
            GroundingThreshold = 0.7m,
            ConsistencyThreshold = 0.6m,
            MinConfidence = 0.7m,
            EnableHumanReview = false,
            GroundingWeight = 0.5m,
            ConsistencyWeight = 0.3m,
            FaithfulnessWeight = 0.2m
        };
    }

    private HallucinationDetector CreateDetector(HallucinationDetectionSettings? settings = null)
    {
        var settingsToUse = settings ?? _defaultSettings;
        var optionsMock = new Mock<IOptions<HallucinationDetectionSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settingsToUse);

        return new HallucinationDetector(optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task DetectAsync_GroundedResponse_ReturnsHighConfidence()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The tower stands 330 meters tall and is located in Paris. " +
                      "It was completed in 1889 as part of the Worlds Fair.";
        var context = "The tower stands 330 meters tall and is located in Paris France. " +
                     "It was completed in 1889 as part of the Worlds Fair and attracts millions of visitors each year.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeGreaterThanOrEqualTo(0.7m);
        result.OverallConfidence.ShouldBeGreaterThanOrEqualTo(0.7m);
        (result.ConfidenceLevel == ConfidenceLevel.Medium || result.ConfidenceLevel == ConfidenceLevel.High).ShouldBeTrue();
        result.RequiresHumanReview.ShouldBeFalse();
        result.ClaimGroundings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DetectAsync_HallucinatedResponse_ReturnsLowConfidence()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Eiffel Tower was built in 1950 and is made entirely of aluminum. It is located in Berlin, Germany.";
        var context = "The Eiffel Tower is a wrought-iron lattice tower on the Champ de Mars in Paris, France. " +
                     "It was completed in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeLessThan(0.7m);
        result.OverallConfidence.ShouldBeLessThan(0.7m);
        result.ConfidenceLevel.ShouldBe(ConfidenceLevel.Low);
        result.Issues.ShouldNotBeEmpty();
        result.Issues.ShouldContain(i => i.Contains("Low context grounding score"));
        result.ClaimGroundings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DetectAsync_EmptyContext_ReturnsZeroGrounding()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Eiffel Tower is located in Paris.";
        var context = "";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBe(0m);
        result.OverallConfidence.ShouldBe(0m);
        result.ConfidenceLevel.ShouldBe(ConfidenceLevel.Low);
        result.RequiresHumanReview.ShouldBeFalse();
        result.ClaimGroundings.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAsync_EmptyResponse_ThrowsArgumentException()
    {
        // Arrange
        var detector = CreateDetector();
        var context = "Some context";

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await detector.DetectAsync("", context));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await detector.DetectAsync("   ", context));
    }

    [Fact]
    public async Task DetectAsync_PartiallyGroundedResponse_ReturnsMediumConfidence()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The tower stands 330 meters tall and is located in Paris. " +
                      "The structure was designed by engineer Gustave Eiffel and completed in 1889. " +
                      "The tower is powered by renewable energy and has a rooftop helipad.";
        var context = "The tower stands 330 meters tall and is located in Paris France. " +
                     "The structure was designed by engineer Gustave Eiffel and completed in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeGreaterThan(0.2m);
        result.ClaimGroundings.Count.ShouldBe(3);
        result.ClaimGroundings.Count(c => c.Score < 0.7m).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DetectAsync_WithHumanReviewEnabled_FlagsLowConfidence()
    {
        // Arrange
        var settings = new HallucinationDetectionSettings
        {
            EnableContextGrounding = true,
            GroundingThreshold = 0.7m,
            MinConfidence = 0.7m,
            EnableHumanReview = true,
            GroundingWeight = 1.0m
        };
        var detector = CreateDetector(settings);
        var response = "The tower is made of plastic and was built last year.";
        var context = "The Eiffel Tower is a wrought-iron structure built in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.RequiresHumanReview.ShouldBeTrue();
        result.OverallConfidence.ShouldBeLessThan(0.7m);
    }

    [Fact]
    public async Task DetectAsync_GroundingDisabled_ReturnsPerfectGrounding()
    {
        // Arrange
        var settings = new HallucinationDetectionSettings
        {
            EnableContextGrounding = false,
            GroundingWeight = 1.0m
        };
        var detector = CreateDetector(settings);
        var response = "Completely made up information that has no grounding.";
        var context = "Real context that doesn't match.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBe(1.0m);
        result.OverallConfidence.ShouldBe(1.0m);
        result.ConfidenceLevel.ShouldBe(ConfidenceLevel.High);
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAsync_SingleSentenceResponse_CalculatesGrounding()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Eiffel Tower stands 330 meters tall.";
        var context = "The tower stands 330 meters (1,083 ft) tall.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.ClaimGroundings.Count.ShouldBe(1);
        result.ClaimGroundings[0].Claim.ShouldBe(response);
        result.ClaimGroundings[0].Score.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task DetectAsync_MultipleSentences_SplitsIntoClaims()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Eiffel Tower is in Paris. It was built in 1889. The tower is made of iron.";
        var context = "The Eiffel Tower is a wrought-iron lattice tower in Paris, France, built in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.ClaimGroundings.Count.ShouldBe(3);
        result.ClaimGroundings.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.Claim));
    }

    [Fact]
    public async Task DetectAsync_VeryShortClaim_UsesWordOverlap()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Paris tower landmark.";
        var context = "The Paris tower landmark structure.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.ClaimGroundings.Count.ShouldBe(1);
        result.GroundingScore.ShouldBeGreaterThan(0.5m);
    }

    [Fact]
    public async Task DetectAsync_CaseInsensitiveMatching_MatchesCorrectly()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "THE EIFFEL TOWER IS IN PARIS.";
        var context = "the eiffel tower is in paris.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeGreaterThan(0.8m);
        result.ClaimGroundings[0].IsGrounded.ShouldBeTrue();
    }

    [Fact]
    public async Task DetectAsync_NGramMatching_CalculatesTrigramAndBigram()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The tower was completed in eighteen eighty nine.";
        var context = "The Eiffel Tower was completed in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.ClaimGroundings.ShouldNotBeEmpty();
        result.ClaimGroundings[0].Score.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task DetectAsync_ComplexResponse_HandlesMultipleClaims()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "Paris is the capital of France and most populous city. " +
                      "The city is home to the Eiffel Tower landmark. " +
                      "The tower was built for the 1889 Worlds Fair event. " +
                      "It attracts millions of visitors from around the world annually.";
        var context = "Paris is the capital of France and most populous city in the country. " +
                     "The city is home to the Eiffel Tower landmark structure. " +
                     "The tower was built for the 1889 Worlds Fair event celebration. " +
                     "It attracts millions of visitors from around the world annually and is globally recognized.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.ClaimGroundings.Count.ShouldBe(4);
        result.GroundingScore.ShouldBeGreaterThan(0.5m);
    }

    [Fact]
    public async Task DetectAsync_ConfidenceLevel_ClassifiesCorrectly()
    {
        // Arrange
        var detector = CreateDetector();

        // High confidence case (> 0.85)
        var highResponse = "The tower stands 330 meters tall and is located in Paris France.";
        var highContext = "The tower stands 330 meters tall and is located in Paris France near the Seine River.";

        // Low confidence case (< 0.7)
        var lowResponse = "The tower was built yesterday from aluminum and plastic materials.";
        var lowContext = "The Eiffel Tower is a wrought iron structure built in 1889.";

        // Act
        var highResult = await detector.DetectAsync(highResponse, highContext);
        var lowResult = await detector.DetectAsync(lowResponse, lowContext);

        // Assert
        highResult.ConfidenceLevel.ShouldBe(ConfidenceLevel.High);
        highResult.OverallConfidence.ShouldBeGreaterThan(0.85m);

        lowResult.ConfidenceLevel.ShouldBe(ConfidenceLevel.Low);
        lowResult.OverallConfidence.ShouldBeLessThan(0.7m);
    }

    [Fact]
    public async Task DetectAsync_WeightedConfidence_CalculatesCorrectly()
    {
        // Arrange
        var settings = new HallucinationDetectionSettings
        {
            EnableContextGrounding = true,
            GroundingWeight = 1.0m,
            ConsistencyWeight = 0m,
            FaithfulnessWeight = 0m,
            GroundingThreshold = 0.7m
        };
        var detector = CreateDetector(settings);
        var response = "The Eiffel Tower is in Paris.";
        var context = "The Eiffel Tower is located in Paris, France.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.OverallConfidence.ShouldBe(result.GroundingScore);
        result.ConsistencyScore.ShouldBeNull();
        result.FaithfulnessScore.ShouldBeNull();
    }

    [Fact]
    public async Task DetectAsync_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HallucinationDetector(null!, _loggerMock.Object));
    }

    [Fact]
    public async Task DetectAsync_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<HallucinationDetectionSettings>>();
        optionsMock.Setup(x => x.Value).Returns(_defaultSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HallucinationDetector(optionsMock.Object, null!));
    }

    [Fact]
    public async Task DetectAsync_IssuesCollection_ContainsRelevantMessages()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The tower is blue and weighs 5 kilograms.";
        var context = "The Eiffel Tower is made of iron and weighs approximately 10,100 tons.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.Issues.ShouldNotBeEmpty();
        result.Issues[0].ShouldContain("Low context grounding score");
        result.Issues[0].ShouldContain("threshold");
    }

    [Fact]
    public async Task DetectAsync_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The Eiffel Tower construction started in 1887 and was completed in 1889.";
        var context = "The Eiffel Tower construction began in 1887 and was completed in 1889.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeGreaterThan(0.5m);
        result.ClaimGroundings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DetectAsync_NumericValues_MatchesCorrectly()
    {
        // Arrange
        var detector = CreateDetector();
        var response = "The tower stands 330 meters tall and weighs 10100 tons.";
        var context = "The tower stands 330 meters tall and weighs approximately 10100 tons.";

        // Act
        var result = await detector.DetectAsync(response, context);

        // Assert
        result.ShouldNotBeNull();
        result.GroundingScore.ShouldBeGreaterThan(0.5m);
    }
}
