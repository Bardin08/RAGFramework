using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Configuration;
using RAG.Application.Interfaces;
using RAG.Application.Services;
using RAG.Core.Domain.Enums;
using Shouldly;

namespace RAG.Tests.Unit.Services;

public class QueryClassifierTests : IDisposable
{
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<QueryClassifier>> _mockLogger;
    private readonly QueryClassificationConfig _config;
    private readonly QueryClassifier _classifier;

    public QueryClassifierTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<QueryClassifier>>();
        _config = new QueryClassificationConfig
        {
            EnableCache = true,
            Timeout = 5000,
            FallbackToHeuristics = true
        };

        _classifier = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(_config),
            _mockLlmProvider.Object);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    #region LLM Classification Tests

    [Fact]
    public async Task ClassifyQueryAsync_ExplicitFactQuery_ReturnsExplicitFact()
    {
        // Arrange
        var query = "What is machine learning?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("explicit_fact");

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ExplicitFact);
        _mockLlmProvider.Verify(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyQueryAsync_ImplicitFactQuery_ReturnsImplicitFact()
    {
        // Arrange
        var query = "Why is RAG effective?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("implicit_fact");

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ImplicitFact);
    }

    [Fact]
    public async Task ClassifyQueryAsync_InterpretableQuery_ReturnsInterpretableRationale()
    {
        // Arrange
        var query = "Compare BM25 and Dense retrieval methods";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("interpretable_rationale");

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.InterpretableRationale);
    }

    [Fact]
    public async Task ClassifyQueryAsync_HiddenRationaleQuery_ReturnsHiddenRationale()
    {
        // Arrange
        var query = "Should we use hybrid search?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("hidden_rationale");

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.HiddenRationale);
    }

    [Theory]
    [InlineData("explicit_fact", QueryType.ExplicitFact)]
    [InlineData("EXPLICIT_FACT", QueryType.ExplicitFact)]
    [InlineData("explicit", QueryType.ExplicitFact)]
    [InlineData("1", QueryType.ExplicitFact)]
    [InlineData("implicit_fact", QueryType.ImplicitFact)]
    [InlineData("implicit", QueryType.ImplicitFact)]
    [InlineData("2", QueryType.ImplicitFact)]
    [InlineData("interpretable_rationale", QueryType.InterpretableRationale)]
    [InlineData("interpretable", QueryType.InterpretableRationale)]
    [InlineData("3", QueryType.InterpretableRationale)]
    [InlineData("hidden_rationale", QueryType.HiddenRationale)]
    [InlineData("hidden", QueryType.HiddenRationale)]
    [InlineData("4", QueryType.HiddenRationale)]
    public async Task ClassifyQueryAsync_VariousResponseFormats_ParsesCorrectly(string llmResponse, QueryType expected)
    {
        // Arrange
        var query = "Test query";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task ClassifyQueryAsync_SameQueryTwice_UsesCache()
    {
        // Arrange
        var query = "What is machine learning?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("explicit_fact");

        // Act
        var result1 = await _classifier.ClassifyQueryAsync(query);
        var result2 = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result1.ShouldBe(QueryType.ExplicitFact);
        result2.ShouldBe(QueryType.ExplicitFact);
        // LLM should only be called once, second call uses cache
        _mockLlmProvider.Verify(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyQueryAsync_CacheDisabled_CallsLlmEveryTime()
    {
        // Arrange
        var config = new QueryClassificationConfig { EnableCache = false };
        var classifierNoCache = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(config),
            _mockLlmProvider.Object);

        var query = "What is machine learning?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("explicit_fact");

        // Act
        await classifierNoCache.ClassifyQueryAsync(query);
        await classifierNoCache.ClassifyQueryAsync(query);

        // Assert
        _mockLlmProvider.Verify(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ClassifyQueryAsync_NormalizedQueries_ShareCache()
    {
        // Arrange
        var query1 = "What is machine learning?";
        var query2 = "  WHAT IS MACHINE LEARNING?  "; // Different casing and whitespace
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("explicit_fact");

        // Act
        var result1 = await _classifier.ClassifyQueryAsync(query1);
        var result2 = await _classifier.ClassifyQueryAsync(query2);

        // Assert
        result1.ShouldBe(QueryType.ExplicitFact);
        result2.ShouldBe(QueryType.ExplicitFact);
        // Should only call LLM once since normalized queries are the same
        _mockLlmProvider.Verify(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public async Task ClassifyQueryAsync_LlmTimeout_FallsBackToHeuristics()
    {
        // Arrange
        var query = "Why is this important?";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ImplicitFact); // "Why" keyword → ImplicitFact by heuristics
    }

    [Fact]
    public async Task ClassifyQueryAsync_LlmThrowsException_FallsBackToHeuristics()
    {
        // Arrange
        var query = "Compare these two approaches";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.InterpretableRationale); // "Compare" keyword → InterpretableRationale
    }

    [Fact]
    public async Task ClassifyQueryAsync_NoLlmProvider_UsesHeuristicsDirectly()
    {
        // Arrange
        var classifierWithoutLlm = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(_config),
            null); // No LLM provider

        var query = "What is Clean Architecture?";

        // Act
        var result = await classifierWithoutLlm.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ExplicitFact); // "What is" keyword → ExplicitFact
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ClassifyQueryAsync_EmptyQuery_ReturnsExplicitFact()
    {
        // Arrange
        var query = "";

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ExplicitFact); // Default for empty
    }

    [Fact]
    public async Task ClassifyQueryAsync_WhitespaceQuery_ReturnsExplicitFact()
    {
        // Arrange
        var query = "   ";

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ExplicitFact); // Default for whitespace
    }

    [Fact]
    public async Task ClassifyQueryAsync_InvalidLlmResponse_ReturnsExplicitFact()
    {
        // Arrange
        var query = "Test query";
        _mockLlmProvider
            .Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid_response_123");

        // Act
        var result = await _classifier.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(QueryType.ExplicitFact); // Default when parsing fails
    }

    #endregion

    #region Heuristic Classifier Tests

    [Theory]
    [InlineData("What is machine learning?", QueryType.ExplicitFact)]
    [InlineData("Who invented the telephone?", QueryType.ExplicitFact)]
    [InlineData("When was Python created?", QueryType.ExplicitFact)]
    [InlineData("Where is the Eiffel Tower?", QueryType.ExplicitFact)]
    [InlineData("Define neural network", QueryType.ExplicitFact)]
    public async Task HeuristicClassifier_ExplicitFactKeywords_ReturnsExplicitFact(string query, QueryType expected)
    {
        // Arrange - Use classifier without LLM to force heuristics
        var classifierWithoutLlm = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(new QueryClassificationConfig { EnableCache = false }),
            null);

        // Act
        var result = await classifierWithoutLlm.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Why is this important?", QueryType.ImplicitFact)]
    [InlineData("How does BM25 work?", QueryType.ImplicitFact)]
    [InlineData("Explain the concept", QueryType.ImplicitFact)]
    [InlineData("Describe the process", QueryType.ImplicitFact)]
    public async Task HeuristicClassifier_ImplicitFactKeywords_ReturnsImplicitFact(string query, QueryType expected)
    {
        // Arrange
        var classifierWithoutLlm = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(new QueryClassificationConfig { EnableCache = false }),
            null);

        // Act
        var result = await classifierWithoutLlm.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Compare BM25 and Dense retrieval", QueryType.InterpretableRationale)]
    [InlineData("Analyze the trade-offs", QueryType.InterpretableRationale)]
    [InlineData("Evaluate different approaches", QueryType.InterpretableRationale)]
    [InlineData("What's the difference between X and Y?", QueryType.InterpretableRationale)]
    public async Task HeuristicClassifier_InterpretableKeywords_ReturnsInterpretableRationale(string query, QueryType expected)
    {
        // Arrange
        var classifierWithoutLlm = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(new QueryClassificationConfig { EnableCache = false }),
            null);

        // Act
        var result = await classifierWithoutLlm.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Should we use hybrid search?", QueryType.HiddenRationale)]
    [InlineData("What's the best approach?", QueryType.HiddenRationale)]
    [InlineData("Would you recommend this?", QueryType.HiddenRationale)]
    [InlineData("What do you think about this?", QueryType.HiddenRationale)]
    public async Task HeuristicClassifier_HiddenRationaleKeywords_ReturnsHiddenRationale(string query, QueryType expected)
    {
        // Arrange
        var classifierWithoutLlm = new QueryClassifier(
            _cache,
            _mockLogger.Object,
            Options.Create(new QueryClassificationConfig { EnableCache = false }),
            null);

        // Act
        var result = await classifierWithoutLlm.ClassifyQueryAsync(query);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion
}
