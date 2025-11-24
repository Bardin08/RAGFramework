using System.Text.Json.Serialization;

namespace RAG.Core.Domain;

/// <summary>
/// Response model from embedding generation service.
/// </summary>
/// <param name="Embeddings">List of embedding vectors. Each vector is a float array with 384 dimensions for all-MiniLM-L6-v2 model.</param>
public record EmbeddingResponse(
    [property: JsonPropertyName("embeddings")] List<float[]> Embeddings)
{
    /// <summary>
    /// Validates the embedding response.
    /// </summary>
    /// <param name="expectedCount">Expected number of embeddings (should match input text count).</param>
    /// <param name="expectedDimension">Expected dimension size of each embedding (default 384 for all-MiniLM-L6-v2).</param>
    /// <exception cref="ArgumentNullException">Thrown when Embeddings is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when embedding count or dimensions don't match expected values.</exception>
    public void Validate(int expectedCount, int expectedDimension = 384)
    {
        if (Embeddings == null)
            throw new ArgumentNullException(nameof(Embeddings), "Embeddings list cannot be null");

        if (Embeddings.Count != expectedCount)
            throw new InvalidOperationException(
                $"Embedding count mismatch: expected {expectedCount}, got {Embeddings.Count}");

        for (int i = 0; i < Embeddings.Count; i++)
        {
            if (Embeddings[i] == null)
                throw new InvalidOperationException($"Embedding at index {i} is null");

            if (Embeddings[i].Length != expectedDimension)
                throw new InvalidOperationException(
                    $"Embedding at index {i} has invalid dimension: expected {expectedDimension}, got {Embeddings[i].Length}");
        }
    }
}
