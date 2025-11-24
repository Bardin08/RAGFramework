using System.Text.Json.Serialization;

namespace RAG.Core.Domain;

/// <summary>
/// Request model for embedding generation service.
/// </summary>
/// <param name="Texts">List of text strings to generate embeddings for. Must not be null or empty.</param>
public record EmbeddingRequest(
    [property: JsonPropertyName("texts")] List<string> Texts)
{
    /// <summary>
    /// Validates the embedding request.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when Texts is null.</exception>
    /// <exception cref="ArgumentException">Thrown when Texts is empty or contains null/empty strings.</exception>
    public void Validate()
    {
        if (Texts == null)
            throw new ArgumentNullException(nameof(Texts), "Texts list cannot be null");

        if (Texts.Count == 0)
            throw new ArgumentException("Texts list cannot be empty", nameof(Texts));

        for (int i = 0; i < Texts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Texts[i]))
                throw new ArgumentException($"Text at index {i} cannot be null or whitespace", nameof(Texts));
        }
    }
}
