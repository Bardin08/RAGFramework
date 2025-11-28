namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for Dense retrieval algorithm using vector similarity search.
/// </summary>
public class DenseSettings
{
    /// <summary>
    /// Default number of top results to return.
    /// Default: 10
    /// </summary>
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Maximum number of top results allowed.
    /// Default: 100
    /// </summary>
    public int MaxTopK { get; set; } = 100;

    /// <summary>
    /// Timeout for total search operations in seconds.
    /// Default: 10 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum cosine similarity threshold for including results (normalized [0, 1] scale).
    /// Results with similarity below this threshold will be filtered out.
    /// Default: 0.5 (50% similarity)
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.5;

    /// <summary>
    /// Timeout for embedding service calls in seconds.
    /// Default: 5 seconds
    /// </summary>
    public int EmbeddingTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Timeout for Qdrant vector store operations in seconds.
    /// Default: 5 seconds
    /// </summary>
    public int QdrantTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Validates the Dense retrieval settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are invalid.</exception>
    public void Validate()
    {
        if (DefaultTopK <= 0)
            throw new InvalidOperationException($"DenseSettings.DefaultTopK must be positive, got {DefaultTopK}");

        if (MaxTopK <= 0)
            throw new InvalidOperationException($"DenseSettings.MaxTopK must be positive, got {MaxTopK}");

        if (DefaultTopK > MaxTopK)
            throw new InvalidOperationException($"DenseSettings.DefaultTopK ({DefaultTopK}) cannot exceed MaxTopK ({MaxTopK})");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException($"DenseSettings.TimeoutSeconds must be positive, got {TimeoutSeconds}");

        if (SimilarityThreshold < 0 || SimilarityThreshold > 1)
            throw new InvalidOperationException($"DenseSettings.SimilarityThreshold must be between 0 and 1, got {SimilarityThreshold}");

        if (EmbeddingTimeoutSeconds <= 0)
            throw new InvalidOperationException($"DenseSettings.EmbeddingTimeoutSeconds must be positive, got {EmbeddingTimeoutSeconds}");

        if (QdrantTimeoutSeconds <= 0)
            throw new InvalidOperationException($"DenseSettings.QdrantTimeoutSeconds must be positive, got {QdrantTimeoutSeconds}");
    }
}
