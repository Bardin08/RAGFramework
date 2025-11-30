using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RAG.Core.Domain;
using RAG.Core.Interfaces;

namespace RAG.Application.Services;

/// <summary>
/// Provides LLM generation with fallback chain and resilience patterns.
/// Implements: Primary (OpenAI) → Secondary (Ollama) → Degraded Mode (context-only).
/// Uses Polly for circuit breaker and retry with exponential backoff.
/// </summary>
public class LLMProviderFallbackService
{
    private readonly IEnumerable<ILLMProvider> _providers;
    private readonly ILogger<LLMProviderFallbackService> _logger;
    private readonly Dictionary<string, AsyncCircuitBreakerPolicy> _circuitBreakerPolicies;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const int MaxRetries = 3;
    private const int CircuitBreakerFailureThreshold = 3;
    private const int CircuitBreakerDurationSeconds = 30;

    public LLMProviderFallbackService(
        IEnumerable<ILLMProvider> providers,
        ILogger<LLMProviderFallbackService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // AC 3: Retry logic with exponential backoff (1s, 2s, 4s)
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientFailure(ex))
            .WaitAndRetryAsync(
                MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)), // 1s, 2s, 4s
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "LLM provider call failed (attempt {RetryCount}/{MaxRetries}). Retrying after {RetryDelay}s",
                        retryCount,
                        MaxRetries,
                        timeSpan.TotalSeconds);
                });

        // AC 2: Circuit breaker per provider - 3 failures → open, half-open after 30s
        _circuitBreakerPolicies = new Dictionary<string, AsyncCircuitBreakerPolicy>();
        foreach (var provider in providers)
        {
            var providerName = provider.ProviderName;
            _circuitBreakerPolicies[providerName] = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    CircuitBreakerFailureThreshold,
                    TimeSpan.FromSeconds(CircuitBreakerDurationSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(
                            exception,
                            "Circuit breaker opened for {ProviderName} after {FailureCount} consecutive failures. Will remain open for {Duration}s",
                            providerName,
                            CircuitBreakerFailureThreshold,
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset for {ProviderName} - normal operation resumed", providerName);
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open for {ProviderName} - testing if service recovered", providerName);
                    });
        }
    }

    /// <summary>
    /// Generates a response using fallback chain with resilience patterns.
    /// AC 1: OpenAI → Ollama → Context-only
    /// </summary>
    public async Task<GenerationResponse> GenerateWithFallbackAsync(
        GenerationRequest request,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // AC 5: Log request context for debugging
        var requestId = Guid.NewGuid();
        _logger.LogInformation(
            "Starting LLM generation with fallback. RequestId: {RequestId}, Query: {Query}",
            requestId,
            request.Query);

        // AC 1: Try providers in order (primary → secondary)
        foreach (var provider in _providers.OrderBy(p => p.ProviderName == "Ollama" ? 1 : 0))
        {
            if (!provider.IsAvailable)
            {
                _logger.LogWarning(
                    "Provider {ProviderName} is not available. Skipping to next provider. RequestId: {RequestId}",
                    provider.ProviderName,
                    requestId);
                continue;
            }

            try
            {
                _logger.LogInformation(
                    "Attempting generation with {ProviderName}. RequestId: {RequestId}",
                    provider.ProviderName,
                    requestId);

                // Apply retry + circuit breaker policies (per-provider circuit breaker)
                var circuitBreaker = _circuitBreakerPolicies[provider.ProviderName];
                var response = await Policy
                    .WrapAsync(_retryPolicy, circuitBreaker)
                    .ExecuteAsync(async () => await provider.GenerateAsync(request, cancellationToken));

                _logger.LogInformation(
                    "Generation successful with {ProviderName}. RequestId: {RequestId}, TokensUsed: {TokensUsed}",
                    provider.ProviderName,
                    requestId,
                    response.TokensUsed);

                return response;
            }
            catch (BrokenCircuitException ex)
            {
                // AC 5: Log full exception details
                _logger.LogError(
                    ex,
                    "Circuit breaker is open for {ProviderName}. Falling back to next provider. RequestId: {RequestId}",
                    provider.ProviderName,
                    requestId);
                // Continue to next provider
            }
            catch (Exception ex)
            {
                // AC 5: Log all exceptions with full details
                _logger.LogError(
                    ex,
                    "Generation failed with {ProviderName}. Error: {ErrorMessage}. RequestId: {RequestId}",
                    provider.ProviderName,
                    ex.Message,
                    requestId);
                // Continue to next provider
            }
        }

        // AC 1: All providers failed → return context-only response (degraded mode)
        _logger.LogWarning(
            "All LLM providers failed. Returning context-only response. RequestId: {RequestId}",
            requestId);

        return CreateContextOnlyResponse(context ?? request.Context, requestId);
    }

    /// <summary>
    /// Creates a degraded mode response when all providers fail.
    /// AC 1: Return retrieved context without generation
    /// AC 4: User-friendly message
    /// </summary>
    private GenerationResponse CreateContextOnlyResponse(string context, Guid requestId)
    {
        var answer = string.IsNullOrWhiteSpace(context)
            ? "No relevant information found" // AC 4: User-friendly message
            : $"Generation failed, showing retrieved context:\n\n{context}"; // AC 4: User-friendly message

        _logger.LogInformation(
            "Created context-only response. RequestId: {RequestId}, HasContext: {HasContext}",
            requestId,
            !string.IsNullOrWhiteSpace(context));

        return new GenerationResponse(
            Answer: answer,
            Model: "context-only",
            TokensUsed: 0,
            ResponseTime: TimeSpan.Zero,
            Sources: new List<SourceReference>()
        );
    }

    /// <summary>
    /// Determines if an exception represents a transient failure worth retrying.
    /// AC 3: Retry only transient failures
    /// </summary>
    private static bool IsTransientFailure(Exception ex)
    {
        return ex is HttpRequestException ||
               ex is TimeoutException ||
               ex is TaskCanceledException ||
               (ex is InvalidOperationException && ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase));
    }
}
