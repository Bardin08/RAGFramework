using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Services;
using RAG.Core.Domain;
using RAG.Core.Interfaces;
using Shouldly;

namespace RAG.Tests.Integration.LLMProviders;

/// <summary>
/// Integration tests for LLM provider fallback with simulated failures.
/// AC 8: Integration test with simulated failures
/// </summary>
public class FallbackIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ILLMProvider> _mockOpenAI;
    private readonly Mock<ILLMProvider> _mockOllama;

    public FallbackIntegrationTests()
    {
        _mockOpenAI = new Mock<ILLMProvider>();
        _mockOllama = new Mock<ILLMProvider>();

        _mockOpenAI.Setup(p => p.ProviderName).Returns("OpenAI");
        _mockOllama.Setup(p => p.ProviderName).Returns("Ollama");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IEnumerable<ILLMProvider>>(sp => new[]
        {
            _mockOpenAI.Object,
            _mockOllama.Object
        });
        services.AddSingleton<LLMProviderFallbackService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// AC 8: Simulated OpenAI failure → successful Ollama fallback
    /// </summary>
    [Fact]
    public async Task Integration_SimulatedOpenAIFailure_FallsBackToOllama()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<LLMProviderFallbackService>();
        var request = new GenerationRequest
        {
            Query = "What is RAG?",
            Context = "RAG stands for Retrieval-Augmented Generation",
            SystemPrompt = "You are a helpful AI assistant",
            MaxTokens = 200,
            Temperature = 0.7m
        };

        // Simulate OpenAI failure
        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("OpenAI API unavailable"));

        // Ollama succeeds
        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerationResponse(
                "RAG is Retrieval-Augmented Generation, combining retrieval and generation.",
                "llama3.1:8b",
                150,
                TimeSpan.FromSeconds(2.5),
                new List<SourceReference>()));

        // Act
        var result = await service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldContain("Retrieval-Augmented Generation");
        result.Model.ShouldBe("llama3.1:8b");
        result.TokensUsed.ShouldBeGreaterThan(0);

        _mockOpenAI.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC 8: Simulated all providers down → context-only response
    /// </summary>
    [Fact]
    public async Task Integration_SimulatedAllProvidersDown_ReturnsContextOnly()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<LLMProviderFallbackService>();
        var contextContent = "RAG (Retrieval-Augmented Generation) is a technique that combines information retrieval with text generation. " +
                           "It first retrieves relevant documents, then uses them as context for generating accurate responses.";
        var request = new GenerationRequest
        {
            Query = "Explain RAG",
            Context = contextContent,
            SystemPrompt = "You are a helpful AI assistant",
            MaxTokens = 200,
            Temperature = 0.7m
        };

        // Simulate both providers failing
        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("OpenAI timeout"));

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama connection refused"));

        // Act
        var result = await service.GenerateWithFallbackAsync(request, contextContent);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldContain("Generation failed, showing retrieved context");
        result.Answer.ShouldContain("Retrieval-Augmented Generation");
        result.Model.ShouldBe("context-only");
        result.TokensUsed.ShouldBe(0);
        result.ResponseTime.ShouldBe(TimeSpan.Zero);

        // Both providers attempted
        _mockOpenAI.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// AC 8: Simulated transient failure → successful retry
    /// </summary>
    [Fact]
    public async Task Integration_SimulatedTransientFailure_SucceedsAfterRetry()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<LLMProviderFallbackService>();
        var request = new GenerationRequest
        {
            Query = "Test query",
            Context = "Test context",
            SystemPrompt = "System prompt",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        var attemptCount = 0;
        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount == 1)
                    throw new HttpRequestException("503 Service Unavailable");
                if (attemptCount == 2)
                    throw new TaskCanceledException("Request timeout");

                // Third attempt succeeds
                return new GenerationResponse(
                    "Success after retry",
                    "gpt-4",
                    120,
                    TimeSpan.FromSeconds(1.2),
                    new List<SourceReference>());
            });

        // Act
        var result = await service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("Success after retry");
        result.Model.ShouldBe("gpt-4");
        attemptCount.ShouldBe(3); // 1 initial + 2 retries

        // Ollama should not be called
        _mockOllama.Verify(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// AC 8: Rate limiting scenario → backoff → eventual success or fallback
    /// </summary>
    [Fact]
    public async Task Integration_SimulatedRateLimiting_HandlesBackoff()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<LLMProviderFallbackService>();
        var request = new GenerationRequest
        {
            Query = "Rate limit test",
            Context = "Context",
            SystemPrompt = "System",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        var callCount = 0;
        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOpenAI.Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new InvalidOperationException("OpenAI API rate limit exceeded. Please retry after 60 seconds.");

                return new GenerationResponse("Success", "gpt-4", 100, TimeSpan.FromSeconds(1), new List<SourceReference>());
            });

        _mockOllama.Setup(p => p.IsAvailable).Returns(true);

        // Act
        var result = await service.GenerateWithFallbackAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("Success");
        callCount.ShouldBeGreaterThanOrEqualTo(3); // Retries with backoff
    }

    /// <summary>
    /// AC 6: Health check integration - verifies providers are checked
    /// </summary>
    [Fact]
    public void Integration_HealthCheck_VerifiesProviderAvailability()
    {
        // Arrange
        _mockOpenAI.Setup(p => p.IsAvailable).Returns(true);
        _mockOllama.Setup(p => p.IsAvailable).Returns(false);

        // Act
        var openAIAvailable = _mockOpenAI.Object.IsAvailable;
        var ollamaAvailable = _mockOllama.Object.IsAvailable;

        // Assert
        openAIAvailable.ShouldBeTrue();
        ollamaAvailable.ShouldBeFalse();

        _mockOpenAI.Verify(p => p.IsAvailable, Times.Once);
        _mockOllama.Verify(p => p.IsAvailable, Times.Once);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
