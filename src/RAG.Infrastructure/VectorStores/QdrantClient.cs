using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;

namespace RAG.Infrastructure.VectorStores;

/// <summary>
/// Qdrant client implementation for vector storage and similarity search.
/// </summary>
public class QdrantClient : IVectorStoreClient
{
    private readonly Qdrant.Client.QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantClient> _logger;
    private readonly string _collectionName;

    public QdrantClient(
        IOptions<QdrantSettings> settings,
        ILogger<QdrantClient> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collectionName = _settings.CollectionName;

        // Parse the Qdrant URL to get host and port
        var uri = new Uri(_settings.Url);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334; // Default Qdrant gRPC port

        // Configure Qdrant client
        _client = string.IsNullOrWhiteSpace(_settings.ApiKey)
            ? new Qdrant.Client.QdrantClient(host, port)
            : new Qdrant.Client.QdrantClient(host, port, apiKey: _settings.ApiKey);

        _logger.LogInformation(
            "Qdrant client initialized. Endpoint: {Host}:{Port}, Collection: {CollectionName}",
            host, port, _collectionName);
    }

    /// <inheritdoc />
    public async Task InitializeCollectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if collection exists
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            var collectionExists = collections.Any(c => c == _collectionName);

            if (collectionExists)
            {
                _logger.LogInformation("Qdrant collection '{CollectionName}' already exists", _collectionName);
                return;
            }

            // Determine distance metric
            var distance = _settings.Distance.ToLowerInvariant() switch
            {
                "cosine" => Distance.Cosine,
                "euclid" => Distance.Euclid,
                "dot" => Distance.Dot,
                _ => Distance.Cosine
            };

            // Create collection with vector configuration
            await _client.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_settings.VectorSize,
                    Distance = distance
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully created Qdrant collection '{CollectionName}' with {VectorSize} dimensions and {Distance} distance",
                _collectionName, _settings.VectorSize, _settings.Distance);

            // Create payload index for tenantId to optimize filtering
            await _client.CreatePayloadIndexAsync(
                collectionName: _collectionName,
                fieldName: "tenantId",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created payload index on 'tenantId' field for tenant filtering");

            // Create payload index for documentId to optimize deletion
            await _client.CreatePayloadIndexAsync(
                collectionName: _collectionName,
                fieldName: "documentId",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created payload index on 'documentId' field for document deletion");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Qdrant collection '{CollectionName}'", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpsertVectorAsync(
        Guid id,
        float[] embedding,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        if (embedding == null || embedding.Length == 0)
        {
            throw new ArgumentException("Embedding cannot be null or empty", nameof(embedding));
        }

        if (embedding.Length != _settings.VectorSize)
        {
            throw new ArgumentException(
                $"Embedding size {embedding.Length} does not match configured vector size {_settings.VectorSize}",
                nameof(embedding));
        }

        try
        {
            var points = new List<PointStruct>
            {
                new PointStruct
                {
                    Id = new PointId { Uuid = id.ToString() },
                    Vectors = embedding,
                    Payload = { ConvertPayload(payload) }
                }
            };

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: points,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Successfully upserted vector {VectorId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert vector {VectorId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task BatchUpsertAsync(
        IEnumerable<(Guid Id, float[] Embedding, Dictionary<string, object> Payload)> vectors,
        CancellationToken cancellationToken = default)
    {
        var vectorList = vectors.ToList();

        if (!vectorList.Any())
        {
            _logger.LogWarning("BatchUpsertAsync called with empty vector list");
            return;
        }

        try
        {
            var points = vectorList.Select(v =>
            {
                if (v.Embedding.Length != _settings.VectorSize)
                {
                    throw new ArgumentException(
                        $"Embedding size {v.Embedding.Length} for vector {v.Id} does not match configured vector size {_settings.VectorSize}");
                }

                return new PointStruct
                {
                    Id = new PointId { Uuid = v.Id.ToString() },
                    Vectors = v.Embedding,
                    Payload = { ConvertPayload(v.Payload) }
                };
            }).ToList();

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: points,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully batch upserted {Count} vectors", vectorList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch upsert {Count} vectors", vectorList.Count);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<(Guid Id, double Score, Dictionary<string, object> Payload)>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
            throw new ArgumentException("Query embedding cannot be null or empty", nameof(queryEmbedding));
        }

        if (queryEmbedding.Length != _settings.VectorSize)
        {
            throw new ArgumentException(
                $"Query embedding size {queryEmbedding.Length} does not match configured vector size {_settings.VectorSize}",
                nameof(queryEmbedding));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));
        }

        try
        {
            // Create filter for tenant isolation
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "tenantId",
                            Match = new Match { Keyword = tenantId.ToString() }
                        }
                    }
                }
            };

            var searchResults = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryEmbedding,
                filter: filter,
                limit: (ulong)topK,
                cancellationToken: cancellationToken);

            var results = searchResults.Select(result =>
            {
                var id = Guid.Parse(result.Id.Uuid);
                var score = result.Score;
                var payload = ConvertPayloadToDict(result.Payload);

                return (id, (double)score, payload);
            }).ToList();

            _logger.LogDebug(
                "Search returned {Count} results for tenant {TenantId}",
                results.Count, tenantId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vectors for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteVectorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var pointIds = new List<PointId>
            {
                new PointId { Uuid = id.ToString() }
            };

            await _client.DeleteAsync(
                collectionName: _collectionName,
                ids: pointIds,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Successfully deleted vector {VectorId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vector {VectorId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteDocumentVectorsAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));
        }

        try
        {
            // Create filter for document and tenant
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId.ToString() }
                        }
                    },
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "tenantId",
                            Match = new Match { Keyword = tenantId.ToString() }
                        }
                    }
                }
            };

            await _client.DeleteAsync(
                collectionName: _collectionName,
                filter: filter,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Deleted all vectors for document {DocumentId} (tenant {TenantId})",
                documentId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete vectors for document {DocumentId} (tenant {TenantId})",
                documentId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Converts a Dictionary payload to Qdrant payload format.
    /// </summary>
    private static Dictionary<string, Value> ConvertPayload(Dictionary<string, object> payload)
    {
        var result = new Dictionary<string, Value>();

        foreach (var kvp in payload)
        {
            result[kvp.Key] = ConvertToValue(kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Converts a C# object to Qdrant Value.
    /// </summary>
    private static Value ConvertToValue(object obj)
    {
        return obj switch
        {
            null => new Value { NullValue = 0 },
            string s => new Value { StringValue = s },
            int i => new Value { IntegerValue = i },
            long l => new Value { IntegerValue = l },
            double d => new Value { DoubleValue = d },
            float f => new Value { DoubleValue = f },
            bool b => new Value { BoolValue = b },
            Guid g => new Value { StringValue = g.ToString() },
            _ => new Value { StringValue = obj.ToString() ?? string.Empty }
        };
    }

    /// <summary>
    /// Converts Qdrant payload back to Dictionary.
    /// </summary>
    private static Dictionary<string, object> ConvertPayloadToDict(
        IDictionary<string, Value> payload)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in payload)
        {
            result[kvp.Key] = ConvertFromValue(kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Converts Qdrant Value to C# object.
    /// </summary>
    private static object ConvertFromValue(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.NullValue => null!,
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            _ => value.ToString()
        };
    }
}
