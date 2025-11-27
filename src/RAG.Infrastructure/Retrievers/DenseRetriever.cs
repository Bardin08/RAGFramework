using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Core.Enums;

namespace RAG.Infrastructure.Retrievers;

/// <summary>
/// Dense retrieval implementation using vector similarity search via Qdrant.
/// Provides semantic search capabilities with embedding-based document retrieval.
/// </summary>
public class DenseRetriever : IRetrievalStrategy
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreClient _vectorStoreClient;
    private readonly DenseSettings _settings;
    private readonly ILogger<DenseRetriever> _logger;

    public DenseRetriever(
        IOptions<DenseSettings> settings,
        IEmbeddingService embeddingService,
        IVectorStoreClient vectorStoreClient,
        ILogger<DenseRetriever> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStoreClient = vectorStoreClient ?? throw new ArgumentNullException(nameof(vectorStoreClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate settings
        _settings.Validate();

        _logger.LogInformation(
            "DenseRetriever initialized. DefaultTopK: {DefaultTopK}, MaxTopK: {MaxTopK}, SimilarityThreshold: {Threshold}",
            _settings.DefaultTopK, _settings.MaxTopK, _settings.SimilarityThreshold);
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> SearchAsync(
        string query,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be greater than 0");
        }

        if (topK > _settings.MaxTopK)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, $"topK cannot exceed {_settings.MaxTopK}");
        }

        _logger.LogDebug(
            "Executing dense retrieval: query='{Query}', topK={TopK}, tenantId={TenantId}",
            query, topK, tenantId);

        try
        {
            // Generate embedding for query with timeout
            float[] queryEmbedding;
            using (var embeddingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                embeddingCts.CancelAfter(TimeSpan.FromSeconds(_settings.EmbeddingTimeoutSeconds));

                try
                {
                    var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                        new List<string> { query },
                        embeddingCts.Token);

                    if (embeddings == null || embeddings.Count == 0)
                    {
                        throw new InvalidOperationException("Embedding service returned no embeddings");
                    }

                    queryEmbedding = embeddings[0];

                    _logger.LogDebug(
                        "Query embedding generated: dimensions={Dimensions}",
                        queryEmbedding.Length);
                }
                catch (OperationCanceledException) when (embeddingCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Embedding generation timed out after {Timeout}s", _settings.EmbeddingTimeoutSeconds);
                    throw new TimeoutException($"Embedding generation timed out after {_settings.EmbeddingTimeoutSeconds} seconds");
                }
            }

            // Execute Qdrant vector search with timeout
            List<(Guid Id, double Score, Dictionary<string, object> Payload)> searchResults;
            using (var qdrantCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                qdrantCts.CancelAfter(TimeSpan.FromSeconds(_settings.QdrantTimeoutSeconds));

                try
                {
                    searchResults = await _vectorStoreClient.SearchAsync(
                        queryEmbedding,
                        topK,
                        tenantId,
                        qdrantCts.Token);

                    _logger.LogDebug(
                        "Qdrant search completed: results={ResultCount}",
                        searchResults.Count);
                }
                catch (OperationCanceledException) when (qdrantCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Qdrant search timed out after {Timeout}s", _settings.QdrantTimeoutSeconds);
                    throw new TimeoutException($"Qdrant search timed out after {_settings.QdrantTimeoutSeconds} seconds");
                }
            }

            // Map results to RetrievalResult with score normalization
            var results = new List<RetrievalResult>();

            foreach (var (id, score, payload) in searchResults)
            {
                // Normalize cosine similarity from [-1, 1] to [0, 1]
                var normalizedScore = NormalizeCosineScore(score);

                // Filter by similarity threshold
                if (normalizedScore < _settings.SimilarityThreshold)
                {
                    _logger.LogDebug(
                        "Filtering result {Id} with score {Score} below threshold {Threshold}",
                        id, normalizedScore, _settings.SimilarityThreshold);
                    continue;
                }

                // Extract payload fields
                var text = payload.GetValueOrDefault("text")?.ToString() ?? string.Empty;
                var documentId = payload.ContainsKey("documentId") && Guid.TryParse(payload["documentId"]?.ToString(), out var docId)
                    ? docId
                    : id;
                var source = payload.GetValueOrDefault("source")?.ToString()
                    ?? payload.GetValueOrDefault("metadata")?.ToString()
                    ?? "Unknown";

                var result = new RetrievalResult(
                    DocumentId: documentId,
                    Score: normalizedScore,
                    Text: text,
                    Source: source,
                    HighlightedText: null // No highlighting for dense retrieval
                );

                results.Add(result);
            }

            _logger.LogInformation(
                "Dense retrieval completed: query='{Query}', results={ResultCount}, topK={TopK}, tenantId={TenantId}",
                query, results.Count, topK, tenantId);

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Embedding service or Qdrant is unavailable");
            throw new InvalidOperationException("External service unavailable for dense retrieval", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Dense retrieval operation was cancelled due to timeout");
            throw new TimeoutException($"Dense retrieval operation timed out after {_settings.TimeoutSeconds} seconds", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not ArgumentOutOfRangeException && ex is not TimeoutException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error during dense retrieval for query '{Query}'", query);
            throw new InvalidOperationException($"Dense retrieval operation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Normalizes cosine similarity score from [-1, 1] range to [0, 1] range.
    /// </summary>
    /// <param name="cosineSimilarity">Cosine similarity score in range [-1, 1].</param>
    /// <returns>Normalized score in range [0, 1].</returns>
    private static double NormalizeCosineScore(double cosineSimilarity)
    {
        // Convert cosine similarity [-1, 1] → [0, 1]
        // -1 (opposite) → 0, 0 (orthogonal) → 0.5, 1 (identical) → 1
        return (cosineSimilarity + 1.0) / 2.0;
    }

    /// <inheritdoc />
    public string GetStrategyName() => "Dense";

    /// <inheritdoc />
    public RetrievalStrategyType StrategyType => RetrievalStrategyType.Dense;
}
