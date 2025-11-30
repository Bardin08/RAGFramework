namespace RAG.Core.Interfaces;

/// <summary>
/// Defines the contract for Large Language Model (LLM) providers.
/// Enables abstraction over different LLM implementations (OpenAI, self-hosted models, etc.)
/// allowing seamless switching between providers without changing application code.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Generates a response asynchronously based on the provided request.
    /// </summary>
    /// <param name="request">The generation request containing query, context, and parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the generation response.</returns>
    Task<Domain.GenerationResponse> GenerateAsync(Domain.GenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response asynchronously, yielding tokens as they are generated.
    /// Enables real-time display of responses without waiting for complete generation.
    /// </summary>
    /// <param name="request">The generation request containing query, context, and parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable stream of generated text tokens.</returns>
    Task<IAsyncEnumerable<string>> GenerateStreamAsync(Domain.GenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the LLM provider (e.g., "OpenAI", "Ollama", "Claude").
    /// Used for logging, monitoring, and provider identification.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether the provider is available and ready to handle requests.
    /// Integrates with ASP.NET Core health checks for monitoring provider readiness.
    /// </summary>
    bool IsAvailable { get; }
}
