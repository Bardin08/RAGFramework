namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a service for generating text embeddings using an external embedding model.
/// </summary>
/// <remarks>
/// Implementations should handle communication with embedding services (e.g., Python-based all-MiniLM-L6-v2 model)
/// and provide resilient, performant embedding generation with appropriate retry logic and connection pooling.
/// </remarks>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates embedding vectors for a batch of text strings asynchronously.
    /// </summary>
    /// <param name="texts">List of text strings to generate embeddings for. Must not be null or empty.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of embedding vectors.</returns>
    /// <remarks>
    /// <para><strong>Batch Processing:</strong> This method supports batch processing with a maximum batch size limit (typically 32 texts for MVP).</para>
    /// <para><strong>Embedding Dimensions:</strong> For the all-MiniLM-L6-v2 model, each embedding vector will have 384 dimensions.</para>
    /// <para><strong>Performance:</strong> Average latency is ~500ms for a batch of 10 texts.</para>
    /// <para><strong>Error Handling:</strong> Implementations should include retry logic with exponential backoff for transient failures.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when texts is null, empty, or exceeds maximum batch size.</exception>
    /// <exception cref="HttpRequestException">Thrown when the embedding service is unavailable after retries.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response embedding count doesn't match input text count.</exception>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
}
