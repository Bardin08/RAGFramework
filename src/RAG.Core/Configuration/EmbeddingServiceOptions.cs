namespace RAG.Core.Configuration;

/// <summary>
/// Configuration options for the embedding service client.
/// </summary>
public class EmbeddingServiceOptions
{
    /// <summary>
    /// Base URL of the Python embedding service. Default is http://localhost:8001.
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:8001";

    /// <summary>
    /// Timeout for HTTP requests in seconds. Default is 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum batch size for embedding requests. Default is 32 texts per request (MVP limit).
    /// </summary>
    public int MaxBatchSize { get; set; } = 32;

    /// <summary>
    /// Maximum number of retry attempts for transient failures. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl))
            throw new InvalidOperationException("ServiceUrl cannot be null or empty");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("TimeoutSeconds must be greater than 0");

        if (MaxBatchSize <= 0)
            throw new InvalidOperationException("MaxBatchSize must be greater than 0");

        if (MaxRetries < 0)
            throw new InvalidOperationException("MaxRetries must be >= 0");
    }
}
