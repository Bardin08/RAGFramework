using System.ComponentModel.DataAnnotations;

namespace RAG.Core.Configuration;

/// <summary>
/// Root configuration settings for the RAG application.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the Elasticsearch settings.
    /// </summary>
    public ElasticsearchSettings Elasticsearch { get; set; } = new();

    /// <summary>
    /// Gets or sets the Qdrant settings.
    /// </summary>
    public QdrantSettings Qdrant { get; set; } = new();

    /// <summary>
    /// Gets or sets the Embedding Service settings.
    /// </summary>
    public EmbeddingServiceSettings EmbeddingService { get; set; } = new();

    /// <summary>
    /// Gets or sets the OpenAI settings.
    /// </summary>
    public OpenAISettings OpenAI { get; set; } = new();

    /// <summary>
    /// Gets or sets the Ollama settings.
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Gets or sets the Keycloak settings.
    /// </summary>
    public KeycloakSettings Keycloak { get; set; } = new();

    /// <summary>
    /// Gets or sets the MinIO settings.
    /// </summary>
    public MinIOSettings MinIO { get; set; } = new();
}

/// <summary>
/// Configuration settings for Elasticsearch.
/// </summary>
public class ElasticsearchSettings
{
    /// <summary>
    /// Gets or sets the Elasticsearch URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Username for Elasticsearch authentication (if required).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Elasticsearch authentication (if required).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Index name for storing document chunks.
    /// </summary>
    [Required]
    public string IndexName { get; set; } = "documents";

    /// <summary>
    /// Number of shards for the index.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas for the index.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// Validates the Elasticsearch settings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ArgumentException("Elasticsearch URL cannot be empty", nameof(Url));
        }

        if (string.IsNullOrWhiteSpace(IndexName))
        {
            throw new ArgumentException("Elasticsearch index name cannot be empty", nameof(IndexName));
        }

        if (NumberOfShards < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(NumberOfShards), "Number of shards must be at least 1");
        }

        if (NumberOfReplicas < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumberOfReplicas), "Number of replicas cannot be negative");
        }
    }
}

/// <summary>
/// Configuration settings for Qdrant vector database.
/// </summary>
public class QdrantSettings
{
    /// <summary>
    /// Gets or sets the Qdrant URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for Qdrant authentication (optional).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the collection name for storing vectors.
    /// </summary>
    [Required]
    public string CollectionName { get; set; } = "document-embeddings";

    /// <summary>
    /// Gets or sets the vector size (dimensionality).
    /// Default is 384 for all-MiniLM-L6-v2 model.
    /// </summary>
    [Range(1, 4096)]
    public int VectorSize { get; set; } = 384;

    /// <summary>
    /// Gets or sets the distance metric for similarity search.
    /// Default is "Cosine" for cosine similarity.
    /// </summary>
    [Required]
    public string Distance { get; set; } = "Cosine";

    /// <summary>
    /// Validates the Qdrant settings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ArgumentException("Qdrant URL cannot be empty", nameof(Url));
        }

        if (string.IsNullOrWhiteSpace(CollectionName))
        {
            throw new ArgumentException("Qdrant collection name cannot be empty", nameof(CollectionName));
        }

        if (VectorSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(VectorSize), "Vector size must be at least 1");
        }

        if (string.IsNullOrWhiteSpace(Distance))
        {
            throw new ArgumentException("Distance metric cannot be empty", nameof(Distance));
        }

        var validDistances = new[] { "Cosine", "Euclid", "Dot" };
        if (!validDistances.Contains(Distance, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Distance metric must be one of: {string.Join(", ", validDistances)}", nameof(Distance));
        }
    }
}

/// <summary>
/// Configuration settings for the Embedding Service.
/// </summary>
public class EmbeddingServiceSettings
{
    /// <summary>
    /// Gets or sets the Embedding Service URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for OpenAI API.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Gets or sets the OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenAI model to use.
    /// </summary>
    [Required]
    public string Model { get; set; } = "gpt-4-turbo";

    /// <summary>
    /// Gets or sets the maximum number of tokens for generation.
    /// </summary>
    [Range(1, 4096)]
    public int MaxTokens { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the temperature for generation (0.0 - 2.0).
    /// </summary>
    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.7f;
}

/// <summary>
/// Configuration settings for Ollama local LLM.
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Gets or sets the Ollama URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Ollama model to use.
    /// </summary>
    [Required]
    public string Model { get; set; } = "llama3.1:8b";
}

/// <summary>
/// Configuration settings for Keycloak authentication.
/// </summary>
public class KeycloakSettings
{
    /// <summary>
    /// Gets or sets the Keycloak authority URL.
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API audience identifier.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for MinIO object storage.
/// </summary>
public class MinIOSettings
{
    /// <summary>
    /// Gets or sets the MinIO endpoint (host:port).
    /// </summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MinIO access key.
    /// </summary>
    [Required]
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MinIO secret key.
    /// </summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bucket name for document storage.
    /// </summary>
    [Required]
    public string BucketName { get; set; } = "rag-documents";

    /// <summary>
    /// Gets or sets whether to use SSL for connections.
    /// </summary>
    public bool UseSSL { get; set; } = false;
}
