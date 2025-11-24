using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;

namespace RAG.Infrastructure.Clients;

/// <summary>
/// HTTP client for communicating with the Python embedding service.
/// </summary>
/// <remarks>
/// Implements resilient communication with retry logic, timeout handling, and connection pooling.
/// Uses all-MiniLM-L6-v2 model via Python service at http://localhost:8001/embed.
/// </remarks>
public class EmbeddingServiceClient : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingServiceOptions _options;
    private readonly ILogger<EmbeddingServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with retry policies and connection pooling.</param>
    /// <param name="options">Configuration options for the embedding service.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public EmbeddingServiceClient(
        HttpClient httpClient,
        IOptions<EmbeddingServiceOptions> options,
        ILogger<EmbeddingServiceClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        _options.Validate();
    }

    /// <inheritdoc/>
    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        // Input validation
        if (texts == null)
            throw new ArgumentNullException(nameof(texts), "Texts list cannot be null");

        if (texts.Count == 0)
            throw new ArgumentException("Texts list cannot be empty", nameof(texts));

        if (texts.Count > _options.MaxBatchSize)
            throw new ArgumentException(
                $"Batch size exceeds maximum limit. Provided: {texts.Count}, Maximum: {_options.MaxBatchSize}",
                nameof(texts));

        // Create request DTO
        var request = new EmbeddingRequest(texts);
        request.Validate();

        _logger.LogInformation(
            "Generating embeddings for batch of {BatchSize} texts",
            texts.Count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // POST request to embedding service
            var response = await _httpClient.PostAsJsonAsync("/embed", request, cancellationToken);

            // Ensure success status code
            response.EnsureSuccessStatusCode();

            // Deserialize response
            var embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken);

            if (embeddingResponse == null)
                throw new InvalidOperationException("Received null response from embedding service");

            // Validate response
            embeddingResponse.Validate(expectedCount: texts.Count, expectedDimension: 384);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully generated {EmbeddingCount} embeddings with dimension {Dimension} in {DurationMs}ms",
                embeddingResponse.Embeddings.Count,
                embeddingResponse.Embeddings[0].Length,
                stopwatch.ElapsedMilliseconds);

            return embeddingResponse.Embeddings;
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Embedding service request timed out after {DurationMs}ms for batch size {BatchSize}",
                stopwatch.ElapsedMilliseconds,
                texts.Count);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "HTTP request to embedding service failed after {DurationMs}ms for batch size {BatchSize}. Service URL: {ServiceUrl}",
                stopwatch.ElapsedMilliseconds,
                texts.Count,
                _options.ServiceUrl);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Embedding response validation failed after {DurationMs}ms. Batch size: {BatchSize}",
                stopwatch.ElapsedMilliseconds,
                texts.Count);
            throw;
        }
    }
}
