using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using RAG.Application.Services;
using RAG.Core.Domain;
using RAG.Core.Interfaces;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for LLMProviderFallbackService.
/// AC 7: Tests for fallback scenarios, circuit breaker, and retry exhaustion.
/// </summary>
public class LLMProviderFallbackServiceTests
{
    private readonly Mock<ILLMProvider> _mockOpenAI;
    private readonly Mock<ILLMProvider> _mockOllama;
    private readonly Mock<ILogger<LLMProviderFallbackService>> _mockLogger;
    private readonly LLMProviderFallbackService _service;

    public LLMProviderFallbackServiceTests()
    {
        _mockOpenAI = new Mock<ILLMProvider>();
        _mockOllama = new Mock<ILLMProvider>();
        _mockLogger = new Mock<ILogger<LLMProviderFallbackService>>();

        _mockOpenAI.Setup(p => p.ProviderName).Returns("OpenAI");
        _mockOllama.Setup(p => p.ProviderName).Returns("Ollama");

        var providers = new List<ILLMProvider> { _mockOpenAI.Object, _mockOllama.Object };
        _service = new LLMProviderFallbackService(providers, _mockLogger.Object);
    }

    /// <summary>
    /// AC 1: Primary provider success - should use OpenAI
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_WhenOpenAISucceeds_ReturnsOpenAIResponse()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "test context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };
        var expectedResponse = new GenerationResponse(
            "OpenAI answer",
            "gpt-4",
            250,
            TimeSpan.FromSeconds(1),
            new List<SourceReference>());

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("OpenAI answer");
        result.Model.ShouldBe("gpt-4");

        _mockOpenAI.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// AC 1: OpenAI failure → fallback to Ollama
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_WhenOpenAIFails_FallsBackToOllama()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "test context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };
        var ollamaResponse = new GenerationResponse(
            "Ollama answer",
            "llama3.1",
            150,
            TimeSpan.FromSeconds(2),
            new List<SourceReference>());

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenAI service unavailable"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollamaResponse);

        // Act
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("Ollama answer");
        result.Model.ShouldBe("llama3.1");

        _mockOpenAI.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC 1: Both providers fail → return context-only response
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_WhenBothProvidersFail_ReturnsContextOnly()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "retrieved context content",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenAI failed"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Ollama timeout"));

        // Act
        var result = await _service.GenerateWithFallbackAsync(request, "retrieved context content");

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldContain("Generation failed, showing retrieved context"); // AC 4
        result.Answer.ShouldContain("retrieved context content");
        result.Model.ShouldBe("context-only");
        result.TokensUsed.ShouldBe(0);
    }

    /// <summary>
    /// AC 1: No context available → "No relevant information found"
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_WhenBothProvidersFailAndNoContext_ReturnsNotFoundMessage()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("OpenAI error"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Ollama error"));

        // Act
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("No relevant information found"); // AC 4
        result.Model.ShouldBe("context-only");
    }

    /// <summary>
    /// AC 1: Provider not available → skip to next provider
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_WhenOllamaUnavailable_SkipsToContextOnly()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("OpenAI failed"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(false); // Not available

        // Act
        var result = await _service.GenerateWithFallbackAsync(request, "context");

        // Assert
        result.ShouldNotBeNull();
        result.Model.ShouldBe("context-only");

        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// AC 3: Retry logic with exponential backoff
    /// Note: This test verifies retry attempts through multiple calls
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_OnTransientFailure_RetriesWithBackoff()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };
        var callCount = 0;

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3) // Fail first 2 attempts
                    throw new HttpRequestException("Transient error");
                return new GenerationResponse("Success", "gpt-4", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());
            });

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);

        // Act
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("Success");
        callCount.ShouldBe(3); // Initial + 2 retries
    }

    /// <summary>
    /// AC 3: After max retries exhausted → fallback to next provider
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_AfterRetryExhaustion_FallsBackToNextProvider()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };
        var ollamaResponse = new GenerationResponse("Ollama", "llama3.1", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Persistent failure")); // Always fails

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ollamaResponse);

        // Act
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("Ollama");
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC 2: Circuit breaker activation after consecutive failures
    /// Note: Testing circuit breaker behavior requires multiple sequential calls
    /// </summary>
    [Fact]
    public async Task GenerateWithFallback_AfterThreeConsecutiveFailures_OpensCircuitBreaker()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test query",
            Context = "context",
            SystemPrompt = "system prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Persistent failure"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Ollama also fails"));

        // Act - Make multiple calls to trigger circuit breaker
        for (int i = 0; i < 3; i++)
        {
            await _service.GenerateWithFallbackAsync(request);
        }

        // Fourth call should trigger circuit breaker
        var result = await _service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Model.ShouldBe("context-only"); // Falls back to degraded mode
    }
}
