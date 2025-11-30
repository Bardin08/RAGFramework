using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Polly;
using Polly.Retry;
using RAG.Core.Domain;
using RAG.Core.Interfaces;
using RAG.Infrastructure.Configuration;
using System.Net;

namespace RAG.Infrastructure.LLMProviders;

/// <summary>
/// OpenAI GPT-4 implementation of ILLMProvider.
/// Provides text generation using OpenAI's Chat Completions API with retry logic and rate limiting.
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIProvider> _logger;
    private readonly ChatClient _chatClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const decimal Gpt4PromptCostPer1K = 0.03m;
    private const decimal Gpt4CompletionCostPer1K = 0.06m;
    private const decimal Gpt4TurboPromptCostPer1K = 0.01m;
    private const decimal Gpt4TurboCompletionCostPer1K = 0.03m;

    public OpenAIProvider(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIProvider> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options.Validate();

        _chatClient = new ChatClient(_options.Model, _options.ApiKey);

        _retryPolicy = Policy
            .Handle<HttpRequestException>(ex =>
            {
                // Retry on transient HTTP errors
                return ex.StatusCode == HttpStatusCode.InternalServerError ||
                       ex.StatusCode == HttpStatusCode.BadGateway ||
                       ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                       ex.StatusCode == HttpStatusCode.GatewayTimeout;
            })
            .WaitAndRetryAsync(
                _options.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "OpenAI API request failed (attempt {RetryCount}/{MaxRetries}). Retrying after {RetryDelay}s",
                        retryCount,
                        _options.MaxRetries,
                        timeSpan.TotalSeconds);
                });
    }

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(_options.ApiKey);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<GenerationResponse> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var startTime = DateTime.UtcNow;

        try
        {
            // Build chat messages
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(request.SystemPrompt)
            };

            // Combine context and query for user message
            var userMessageContent = string.IsNullOrWhiteSpace(request.Context)
                ? request.Query
                : $"Context:\n{request.Context}\n\nQuestion: {request.Query}";

            messages.Add(new UserChatMessage(userMessageContent));

            _logger.LogInformation(
                "Sending request to OpenAI API. Model: {Model}, Temperature: {Temperature}, MaxTokens: {MaxTokens}",
                _options.Model,
                request.Temperature,
                request.MaxTokens);

            // Execute with retry policy
            var chatCompletion = await _retryPolicy.ExecuteAsync(async () =>
            {
                var options = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = request.MaxTokens,
                    Temperature = (float)request.Temperature
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                return await _chatClient.CompleteChatAsync(messages, options, cts.Token);
            });

            var responseTime = DateTime.UtcNow - startTime;

            // Extract response
            var answer = chatCompletion.Value.Content[0].Text;
            var usage = chatCompletion.Value.Usage;
            var tokensUsed = usage.TotalTokenCount;

            // Calculate cost
            var cost = CalculateCost(usage.InputTokenCount, usage.OutputTokenCount, _options.Model);

            _logger.LogInformation(
                "OpenAI API request completed. Tokens: {TotalTokens} (Prompt: {PromptTokens}, Completion: {CompletionTokens}), " +
                "Cost: ${Cost:F4}, Duration: {Duration}ms",
                tokensUsed,
                usage.InputTokenCount,
                usage.OutputTokenCount,
                cost,
                responseTime.TotalMilliseconds);

            // Extract sources from context (simple implementation - can be enhanced)
            var sources = ExtractSources(request.Context);

            return new GenerationResponse(
                Answer: answer,
                Model: _options.Model,
                TokensUsed: tokensUsed,
                ResponseTime: responseTime,
                Sources: sources
            );
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Handle rate limiting
            var retryAfter = TimeSpan.FromSeconds(60); // Default to 60s if header not available

            _logger.LogWarning(
                "OpenAI API rate limit exceeded. Retry after {RetryAfter}s",
                retryAfter.TotalSeconds);

            throw new InvalidOperationException(
                $"OpenAI API rate limit exceeded. Please retry after {retryAfter.TotalSeconds} seconds.",
                ex);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("OpenAI API request cancelled or timed out after {Timeout}s", _options.TimeoutSeconds);
            throw new TimeoutException($"OpenAI API request timed out after {_options.TimeoutSeconds} seconds", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API request failed unexpectedly");
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<IAsyncEnumerable<string>> GenerateStreamAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Streaming implementation will be added in Story 5.7
        throw new NotImplementedException("Streaming generation will be implemented in Story 5.7");
    }

    /// <summary>
    /// Calculates the estimated cost based on token usage and model pricing.
    /// </summary>
    private decimal CalculateCost(int promptTokens, int completionTokens, string model)
    {
        var (promptCost, completionCost) = model.ToLowerInvariant() switch
        {
            "gpt-4" => (Gpt4PromptCostPer1K, Gpt4CompletionCostPer1K),
            "gpt-4-turbo" or "gpt-4-turbo-preview" => (Gpt4TurboPromptCostPer1K, Gpt4TurboCompletionCostPer1K),
            _ => (Gpt4TurboPromptCostPer1K, Gpt4TurboCompletionCostPer1K) // Default to GPT-4 Turbo pricing
        };

        return (promptTokens * promptCost + completionTokens * completionCost) / 1000m;
    }

    /// <summary>
    /// Extracts source references from the context string.
    /// Simple implementation - can be enhanced with proper source tracking.
    /// </summary>
    private List<SourceReference> ExtractSources(string context)
    {
        // For now, return empty list
        // Future enhancement: Parse context to extract actual source references
        return new List<SourceReference>();
    }
}
