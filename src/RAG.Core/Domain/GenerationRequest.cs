namespace RAG.Core.Domain;

/// <summary>
/// Represents a request for text generation from the LLM.
/// </summary>
public record GenerationRequest
{
    /// <summary>
    /// The user's question or prompt.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Retrieved context for generation. Can be empty for zero-shot queries.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; init; } = 500;

    /// <summary>
    /// Sampling temperature for generation (0.0-1.0).
    /// Lower values make output more deterministic, higher values more creative.
    /// </summary>
    public decimal Temperature { get; init; } = 0.7m;

    /// <summary>
    /// System-level instructions for the LLM.
    /// Provides default RAG instructions for context-based generation.
    /// </summary>
    public string SystemPrompt { get; init; } = "You are a helpful assistant. Answer the user's question based on the provided context. If the context doesn't contain relevant information, say so.";
}
