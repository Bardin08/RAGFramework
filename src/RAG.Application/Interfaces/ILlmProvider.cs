namespace RAG.Application.Interfaces;

/// <summary>
/// Interface for LLM providers (Ollama, OpenAI, etc.).
/// This is a placeholder interface for Epic 4 query classification.
/// Full implementation will be in Epic 5.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generates text completion from the LLM.
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated text response</returns>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
