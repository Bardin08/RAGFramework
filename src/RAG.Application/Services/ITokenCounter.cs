namespace RAG.Application.Services;

/// <summary>
/// Interface for counting tokens in text for LLM context management.
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Counts the number of tokens in the given text.
    /// </summary>
    /// <param name="text">Text to count tokens in.</param>
    /// <returns>Estimated token count.</returns>
    int CountTokens(string text);
}
