using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;

namespace RAG.Infrastructure.SearchEngines;

/// <summary>
/// Elasticsearch client implementation for document indexing and search.
/// </summary>
public class ElasticsearchClient : ISearchEngineClient
{
    private readonly ElasticsearchClientSettings _client;
    private readonly ElasticsearchSettings _settings;
    private readonly ILogger<ElasticsearchClient> _logger;
    private readonly string _indexName;

    public ElasticsearchClient(
        IOptions<ElasticsearchSettings> settings,
        ILogger<ElasticsearchClient> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexName = _settings.IndexName;

        // Configure Elasticsearch client
        var clientSettings = new ElasticsearchClientSettings(new Uri(_settings.Url));

        // Disable SSL certificate validation for HTTPS connections (for development/testing with self-signed certs)
        if (_settings.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            clientSettings = clientSettings.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
        {
            clientSettings = clientSettings.Authentication(new BasicAuthentication(_settings.Username, _settings.Password));
        }

        _client = clientSettings;
        
        _logger.LogInformation(
            "Elasticsearch client initialized. Endpoint: {Url}, Index: {IndexName}",
            _settings.Url, _indexName);
    }

    /// <inheritdoc />
    public async Task InitializeIndexAsync(CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);
        
        // Check if index exists
        var existsResponse = await client.Indices.ExistsAsync(_indexName, cancellationToken);
        
        if (existsResponse.Exists)
        {
            _logger.LogInformation("Elasticsearch index '{IndexName}' already exists", _indexName);
            return;
        }

        // Create index with mapping
        var createIndexResponse = await client.Indices.CreateAsync(_indexName, c => c
            .Settings(s => s
                .NumberOfShards(_settings.NumberOfShards)
                .NumberOfReplicas(_settings.NumberOfReplicas))
            .Mappings(m => m
                .Properties<DocumentChunk>(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.DocumentId)
                    .Keyword(k => k.TenantId)
                    .Text(k => k.Text, t => t
                        .Analyzer("standard"))
                    .IntegerNumber(k => k.StartIndex)
                    .IntegerNumber(k => k.EndIndex)
                    .IntegerNumber(k => k.ChunkIndex)
                    .Object(k => k.Metadata))),
            cancellationToken);

        if (!createIndexResponse.IsValidResponse)
        {
            _logger.LogError(
                "Failed to create Elasticsearch index '{IndexName}': {Error}",
                _indexName, createIndexResponse.DebugInformation);
            throw new InvalidOperationException($"Failed to create Elasticsearch index: {createIndexResponse.DebugInformation}");
        }

        _logger.LogInformation("Successfully created Elasticsearch index '{IndexName}'", _indexName);
    }

    /// <inheritdoc />
    public async Task IndexDocumentAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);

        var response = await client.IndexAsync(chunk, idx => idx
            .Index(_indexName)
            .Id(chunk.Id.ToString()),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to index document chunk {ChunkId}: {Error}",
                chunk.Id, response.DebugInformation);
            throw new InvalidOperationException($"Failed to index document: {response.DebugInformation}");
        }

        _logger.LogDebug("Successfully indexed chunk {ChunkId} for document {DocumentId}", chunk.Id, chunk.DocumentId);
    }

    /// <inheritdoc />
    public async Task BulkIndexAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);
        var chunkList = chunks.ToList();

        if (!chunkList.Any())
        {
            _logger.LogWarning("BulkIndexAsync called with empty chunk list");
            return;
        }

        var bulkResponse = await client.BulkAsync(b => b
            .Index(_indexName)
            .IndexMany(chunkList, (descriptor, chunk) => descriptor
                .Id(chunk.Id.ToString())),
            cancellationToken);

        if (!bulkResponse.IsValidResponse || bulkResponse.Errors)
        {
            var errorMessages = bulkResponse.ItemsWithErrors.Select(i => i.Error?.Reason).ToList();
            _logger.LogError(
                "Bulk indexing failed for {ErrorCount} out of {TotalCount} chunks. Errors: {Errors}",
                bulkResponse.ItemsWithErrors.Count(), chunkList.Count, string.Join("; ", errorMessages));
            
            if (!bulkResponse.IsValidResponse)
            {
                throw new InvalidOperationException($"Bulk indexing failed: {bulkResponse.DebugInformation}");
            }
        }

        _logger.LogInformation("Successfully bulk indexed {Count} chunks", chunkList.Count);
    }

    /// <inheritdoc />
    public async Task<List<(DocumentChunk Chunk, double Score)>> SearchAsync(
        string query,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);
        
        var searchResponse = await client.SearchAsync<DocumentChunk>(s => s
            .Indices(_indexName)
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
                            .Value(tenantId.ToString()))))),
            cancellationToken);

        if (!searchResponse.IsValidResponse)
        {
            _logger.LogError(
                "Search failed for query '{Query}': {Error}",
                query, searchResponse.DebugInformation);
            throw new InvalidOperationException($"Search failed: {searchResponse.DebugInformation}");
        }

        var results = searchResponse.Hits
            .Select(hit => (hit.Source, hit.Score ?? 0.0))
            .Where(r => r.Source != null)
            .Select(r => (r.Source!, r.Item2))
            .ToList();

        _logger.LogDebug(
            "Search for '{Query}' returned {Count} results for tenant {TenantId}",
            query, results.Count, tenantId);

        return results;
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(Guid chunkId, CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);
        
        var response = await client.DeleteAsync(_indexName, chunkId.ToString(), cancellationToken);

        if (!response.IsValidResponse)
        {
            // Log but don't throw if document wasn't found
            if (response.ElasticsearchServerError?.Status == 404)
            {
                _logger.LogWarning("Chunk {ChunkId} not found for deletion", chunkId);
                return;
            }

            _logger.LogError(
                "Failed to delete chunk {ChunkId}: {Error}",
                chunkId, response.DebugInformation);
            throw new InvalidOperationException($"Failed to delete document: {response.DebugInformation}");
        }

        _logger.LogDebug("Successfully deleted chunk {ChunkId}", chunkId);
    }

    /// <inheritdoc />
    public async Task DeleteDocumentChunksAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(_client);
        
        var response = await client.DeleteByQueryAsync<DocumentChunk>(_indexName, d => d
            .Query(q => q
                .Bool(b => b
                    .Filter(f => f
                        .Term(t => t
                            .Field(fi => fi.DocumentId)
                            .Value(documentId.ToString())),
                        f => f
                        .Term(t => t
                            .Field(fi => fi.TenantId)
                            .Value(tenantId.ToString()))))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to delete chunks for document {DocumentId}: {Error}",
                documentId, response.DebugInformation);
            throw new InvalidOperationException($"Failed to delete document chunks: {response.DebugInformation}");
        }

        _logger.LogInformation(
            "Deleted {Count} chunks for document {DocumentId} (tenant {TenantId})",
            response.Deleted, documentId, tenantId);
    }
}
