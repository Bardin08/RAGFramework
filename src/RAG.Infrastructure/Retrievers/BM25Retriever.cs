using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Core.Enums;

namespace RAG.Infrastructure.Retrievers;

/// <summary>
/// BM25-based document retrieval using Elasticsearch.
/// Provides keyword-based search with query highlighting.
/// </summary>
public class BM25Retriever : IRetrievalStrategy
{
    private readonly ElasticsearchClient _client;
    private readonly BM25Settings _settings;
    private readonly ElasticsearchSettings _elasticsearchSettings;
    private readonly ILogger<BM25Retriever> _logger;

    public BM25Retriever(
        IOptions<BM25Settings> settings,
        IOptions<ElasticsearchSettings> elasticsearchSettings,
        ILogger<BM25Retriever> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _elasticsearchSettings = elasticsearchSettings?.Value ?? throw new ArgumentNullException(nameof(elasticsearchSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate settings
        _settings.Validate();
        _elasticsearchSettings.Validate();

        // Configure Elasticsearch client
        var clientSettings = new ElasticsearchClientSettings(new Uri(_elasticsearchSettings.Url))
            .RequestTimeout(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        // Disable SSL certificate validation for HTTPS connections (for development/testing with self-signed certs)
        if (_elasticsearchSettings.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            clientSettings = clientSettings.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
        }

        if (!string.IsNullOrWhiteSpace(_elasticsearchSettings.Username) && !string.IsNullOrWhiteSpace(_elasticsearchSettings.Password))
        {
            clientSettings = clientSettings.Authentication(new BasicAuthentication(_elasticsearchSettings.Username, _elasticsearchSettings.Password));
        }

        _client = new ElasticsearchClient(clientSettings);

        _logger.LogInformation(
            "BM25Retriever initialized. Elasticsearch: {Url}, Index: {IndexName}, K1: {K1}, B: {B}",
            _elasticsearchSettings.Url, _elasticsearchSettings.IndexName, _settings.K1, _settings.B);
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
            "Executing BM25 search: query='{Query}', topK={TopK}, tenantId={TenantId}",
            query, topK, tenantId);

        try
        {
            // Execute Elasticsearch search with highlighting
            var searchResponse = await _client.SearchAsync<DocumentChunk>(s => s
                .Index(_elasticsearchSettings.IndexName)
                .Size(topK)
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Match(ma => ma
                                .Field(f => f.Text)
                                .Query(query)))
                        .Filter(f => f
                            .Term(t => t
                                .Field(fi => fi.TenantId)
                                .Value(tenantId.ToString())))))
                .Highlight(h => h
                    .Fields(f => f
                        .Add("text", new HighlightField
                        {
                            FragmentSize = _settings.HighlightFragmentSize,
                            PreTags = ["<em>"],
                            PostTags = ["</em>"],
                            NumberOfFragments = 1
                        }))),
                cancellationToken);

            // Handle Elasticsearch errors
            if (!searchResponse.IsValidResponse)
            {
                var errorMessage = searchResponse.ElasticsearchServerError?.Error?.Reason
                    ?? searchResponse.DebugInformation;

                _logger.LogError(
                    "Elasticsearch search failed for query '{Query}': {Error}",
                    query, errorMessage);

                // Check if it's a timeout
                if (searchResponse.ApiCallDetails?.HttpStatusCode == 408 ||
                    errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    throw new TimeoutException($"Search operation timed out after {_settings.TimeoutSeconds} seconds");
                }

                throw new InvalidOperationException($"Elasticsearch search failed: {errorMessage}");
            }

            // Map Elasticsearch hits to RetrievalResult
            var results = new List<RetrievalResult>();

            foreach (var hit in searchResponse.Hits)
            {
                if (hit.Source == null)
                    continue;

                // Extract highlighted text if available
                string? highlightedText = null;
                if (hit.Highlight != null && hit.Highlight.TryGetValue("text", out var highlights))
                {
                    highlightedText = highlights.FirstOrDefault();
                }

                var result = new RetrievalResult(
                    DocumentId: hit.Source.DocumentId,
                    Score: hit.Score ?? 0.0,
                    Text: hit.Source.Text,
                    Source: hit.Source.Metadata?.GetValueOrDefault("source")?.ToString() ?? "Unknown",
                    HighlightedText: highlightedText
                );

                results.Add(result);
            }

            _logger.LogInformation(
                "BM25 search completed: query='{Query}', results={ResultCount}, topK={TopK}, tenantId={TenantId}",
                query, results.Count, topK, tenantId);

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Elasticsearch is unavailable at {Url}", _elasticsearchSettings.Url);
            throw new InvalidOperationException($"Elasticsearch is unavailable at {_elasticsearchSettings.Url}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Search operation was cancelled");
            throw new TimeoutException($"Search operation timed out after {_settings.TimeoutSeconds} seconds", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not ArgumentOutOfRangeException && ex is not TimeoutException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error during BM25 search for query '{Query}'", query);
            throw new InvalidOperationException($"Search operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public string GetStrategyName() => "BM25";

    /// <inheritdoc />
    public RetrievalStrategyType StrategyType => RetrievalStrategyType.BM25;
}
