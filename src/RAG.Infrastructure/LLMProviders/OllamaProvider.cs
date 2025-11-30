using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Domain;
using RAG.Core.Interfaces;
using RAG.Infrastructure.Configuration;
using RAG.Infrastructure.DTOs;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RAG.Infrastructure.LLMProviders;

/// <summary>
/// Ollama-based LLM provider for self-hosted Llama models.
/// Provides text generation using Ollama's REST API with streaming support.
/// </summary>
public class OllamaProvider : ILLMProvider
{
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaProvider> _logger;
    private readonly HttpClient _httpClient;
    private DateTime _lastAvailabilityCheck = DateTime.MinValue;
    private bool _isAvailableCache = false;
    private readonly TimeSpan _availabilityCacheDuration = TimeSpan.FromSeconds(60);

    public OllamaProvider(
        IOptions<OllamaOptions> options,
        ILogger<OllamaProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options.Validate();

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <inheritdoc/>
    public string ProviderName => "Ollama";

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            // Use cached result if still valid
            if (DateTime.UtcNow - _lastAvailabilityCheck < _availabilityCacheDuration)
            {
                return _isAvailableCache;
            }

            try
            {
                // Check Ollama service health
                var response = _httpClient.GetAsync("/api/tags").Result;
                if (!response.IsSuccessStatusCode)
                {
                    _isAvailableCache = false;
                    _lastAvailabilityCheck = DateTime.UtcNow;
                    return false;
                }

                // Verify model is available
                var tagsResponse = response.Content.ReadFromJsonAsync<OllamaTagsResponse>().Result;
                var modelAvailable = tagsResponse?.Models?.Any(m => m.Name == _options.Model) ?? false;

                _isAvailableCache = modelAvailable;
                _lastAvailabilityCheck = DateTime.UtcNow;

                if (!modelAvailable)
                {
                    _logger.LogWarning("Ollama model '{Model}' not found. Available models: {Models}",
                        _options.Model,
                        string.Join(", ", tagsResponse?.Models?.Select(m => m.Name) ?? Array.Empty<string>()));
                }

                return modelAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Ollama availability");
                _isAvailableCache = false;
                _lastAvailabilityCheck = DateTime.UtcNow;
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
            // Build prompt (Ollama uses simple string prompt, not chat messages)
            var prompt = BuildPrompt(request);

            var ollamaRequest = new OllamaRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaRequest.OllamaOptions
                {
                    Temperature = (float)request.Temperature,
                    NumPredict = request.MaxTokens
                }
            };

            _logger.LogInformation(
                "Sending request to Ollama API. Model: {Model}, Temperature: {Temperature}, NumPredict: {NumPredict}, PromptLength: {PromptLength}",
                _options.Model,
                request.Temperature,
                request.MaxTokens,
                prompt.Length);

            var response = await _httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);

            if (ollamaResponse == null)
                throw new InvalidOperationException("Received null response from Ollama API");

            var responseTime = DateTime.UtcNow - startTime;

            // Calculate tokens (Ollama provides eval_count)
            var tokensUsed = ollamaResponse.PromptEvalCount + ollamaResponse.EvalCount;

            _logger.LogInformation(
                "Ollama generation completed. Model: {Model}, ResponseTime: {ResponseTime}ms, PromptTokens: {PromptTokens}, CompletionTokens: {CompletionTokens}, TotalTokens: {TotalTokens}",
                _options.Model,
                responseTime.TotalMilliseconds,
                ollamaResponse.PromptEvalCount,
                ollamaResponse.EvalCount,
                tokensUsed);

            // Extract sources (simple implementation)
            var sources = new List<SourceReference>();

            return new GenerationResponse(
                Answer: ollamaResponse.Response,
                Model: _options.Model,
                TokensUsed: tokensUsed,
                ResponseTime: responseTime,
                Sources: sources
            );
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("Ollama API request timed out after {Timeout}s", _options.TimeoutSeconds);
            throw new TimeoutException($"Ollama API request timed out after {_options.TimeoutSeconds} seconds", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama API request failed");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama generation failed unexpectedly");
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<IAsyncEnumerable<string>> GenerateStreamAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return Task.FromResult(GenerateStreamInternalAsync(request, cancellationToken));
    }

    private async IAsyncEnumerable<string> GenerateStreamInternalAsync(
        GenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        DateTime? firstTokenTime = null;

        var prompt = BuildPrompt(request);

        var ollamaRequest = new OllamaRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            Stream = true,
            Options = new OllamaRequest.OllamaOptions
            {
                Temperature = (float)request.Temperature,
                NumPredict = request.MaxTokens
            }
        };

        _logger.LogInformation(
            "Starting streaming request to Ollama. Model: {Model}, PromptLength: {PromptLength}, Temperature: {Temperature}, MaxTokens: {MaxTokens}",
            _options.Model,
            prompt.Length,
            request.Temperature,
            request.MaxTokens);

        _logger.LogDebug("Ollama request: Stream={Stream}, HasSystemPrompt={HasSystem}",
            ollamaRequest.Stream,
            !string.IsNullOrEmpty(request.SystemPrompt));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Ollama API returned error. StatusCode: {StatusCode}, Response: {ErrorContent}",
                    response.StatusCode,
                    errorContent);
            }

            response.EnsureSuccessStatusCode();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("Ollama streaming request cancelled or timed out");
            throw new TimeoutException("Ollama streaming request timed out", ex);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var tokenCount = 0;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var streamResponse = JsonSerializer.Deserialize<OllamaStreamResponse>(line);
            if (streamResponse == null)
                continue;

            if (!string.IsNullOrEmpty(streamResponse.Response))
            {
                if (!firstTokenTime.HasValue)
                {
                    firstTokenTime = DateTime.UtcNow;
                    var ttft = (firstTokenTime.Value - startTime).TotalMilliseconds;
                    _logger.LogInformation("Time to first token (TTFT): {TTFT}ms", ttft);
                }

                tokenCount++;
                yield return streamResponse.Response;
            }

            if (streamResponse.Done)
            {
                var totalTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Ollama streaming completed. Model: {Model}, TotalTime: {TotalTime}ms, Tokens: {Tokens}",
                    _options.Model,
                    totalTime.TotalMilliseconds,
                    tokenCount);
                break;
            }
        }
    }

    private string BuildPrompt(GenerationRequest request)
    {
        // Ollama uses simple string prompts, not chat message arrays
        var promptParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            promptParts.Add(request.SystemPrompt);
        }

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            promptParts.Add($"Context:\n{request.Context}");
        }

        promptParts.Add($"Question: {request.Query}");
        promptParts.Add("Answer:");

        return string.Join("\n\n", promptParts);
    }

    private class OllamaTagsResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("models")]
        public List<ModelInfo>? Models { get; set; }

        public class ModelInfo
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
