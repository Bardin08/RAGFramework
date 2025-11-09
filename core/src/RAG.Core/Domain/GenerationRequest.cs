namespace RAG.Core.Domain;

/// <summary>
/// Represents a request for text generation from the LLM.
/// </summary>
public record GenerationRequest
{
    /// <summary>
    /// The user's query text.
    /// </summary>
    public string Query { get; init; }

    /// <summary>
    /// List of retrieved documents to use as context.
    /// </summary>
    public List<RetrievalResult> RetrievedDocuments { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate.
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// The temperature parameter for generation (0.0-2.0).
    /// </summary>
    public float Temperature { get; init; }

    /// <summary>
    /// Creates a new GenerationRequest instance with validation.
    /// </summary>
    public GenerationRequest(string query, List<RetrievalResult> retrievedDocuments, int maxTokens, float temperature)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (retrievedDocuments == null)
            throw new ArgumentNullException(nameof(retrievedDocuments));

        if (maxTokens <= 0)
            throw new ArgumentException("MaxTokens must be greater than 0", nameof(maxTokens));

        if (temperature < 0.0f || temperature > 2.0f)
            throw new ArgumentException("Temperature must be between 0.0 and 2.0", nameof(temperature));

        Query = query;
        RetrievedDocuments = retrievedDocuments;
        MaxTokens = maxTokens;
        Temperature = temperature;
    }
}
