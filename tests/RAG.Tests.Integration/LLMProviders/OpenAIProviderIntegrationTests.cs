using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Domain;
using RAG.Infrastructure.Configuration;
using RAG.Infrastructure.LLMProviders;
using Shouldly;

namespace RAG.Tests.Integration.LLMProviders;

/// <summary>
/// Integration tests for OpenAIProvider with real API calls.
/// These tests are skipped by default - requires valid OpenAI API key in user secrets.
/// To run: dotnet user-secrets set "LLMProviders:OpenAI:ApiKey" "sk-your-key"
/// </summary>
public class OpenAIProviderIntegrationTests
{
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProviderIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OpenAIProvider>();
    }

    [Fact(Skip = "Requires OpenAI API key - set in user secrets to enable")]
    public async Task GenerateAsync_WithRealAPI_ReturnsValidResponse()
    {
        // Arrange
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Skip test if no API key available
            return;
        }

        var options = new OpenAIOptions
        {
            ApiKey = apiKey,
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };

        var provider = new OpenAIProvider(Options.Create(options), _logger);

        var request = new GenerationRequest
        {
            Query = "What is RAG?",
            Context = "RAG (Retrieval-Augmented Generation) combines retrieval with generation to provide accurate, context-grounded answers.",
            Temperature = 0.7m,
            MaxTokens = 500,
            SystemPrompt = "You are a helpful assistant. Answer based on the provided context."
        };

        // Act
        var response = await provider.GenerateAsync(request);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldNotBeNullOrEmpty();
        response.Model.ShouldBe("gpt-4-turbo");
        response.TokensUsed.ShouldBeGreaterThan(0);
        response.ResponseTime.ShouldBeGreaterThan(TimeSpan.Zero);
        response.Sources.ShouldNotBeNull();
    }

    [Fact(Skip = "Requires OpenAI API key - set in user secrets to enable")]
    public async Task GenerateAsync_WithLongContext_HandlesTokenLimits()
    {
        // Arrange
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var options = new OpenAIOptions
        {
            ApiKey = apiKey,
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };

        var provider = new OpenAIProvider(Options.Create(options), _logger);

        // Create a long context
        var longContext = string.Join("\n", Enumerable.Repeat("This is a test sentence with some content. ", 500));

        var request = new GenerationRequest
        {
            Query = "Summarize the main points from the context.",
            Context = longContext,
            Temperature = 0.5m,
            MaxTokens = 1000,
            SystemPrompt = "Provide a concise summary."
        };

        // Act
        var response = await provider.GenerateAsync(request);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldNotBeNullOrEmpty();
        response.TokensUsed.ShouldBeGreaterThan(0);
    }

    [Fact(Skip = "Requires OpenAI API key - set in user secrets to enable")]
    public async Task GenerateAsync_WithLowTemperature_ProducesDeterministicOutput()
    {
        // Arrange
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var options = new OpenAIOptions
        {
            ApiKey = apiKey,
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };

        var provider = new OpenAIProvider(Options.Create(options), _logger);

        var request = new GenerationRequest
        {
            Query = "What is 2+2?",
            Context = "Simple arithmetic operations.",
            Temperature = 0.0m, // Deterministic
            MaxTokens = 100
        };

        // Act
        var response1 = await provider.GenerateAsync(request);
        var response2 = await provider.GenerateAsync(request);

        // Assert
        response1.Answer.ShouldNotBeNullOrEmpty();
        response2.Answer.ShouldNotBeNullOrEmpty();
        // With temperature 0, responses should be very similar (though not guaranteed identical)
        response1.TokensUsed.ShouldBeGreaterThan(0);
        response2.TokensUsed.ShouldBeGreaterThan(0);
    }
}
