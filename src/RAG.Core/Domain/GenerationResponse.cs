namespace RAG.Core.Domain;

/// <summary>
/// Represents a response from the LLM generation service.
/// </summary>
/// <param name="Answer">The generated answer text.</param>
/// <param name="Sources">List of source citations used in the answer.</param>
/// <param name="Model">The LLM model used for generation.</param>
/// <param name="TokensUsed">The actual number of tokens consumed.</param>
/// <param name="ResponseTime">The time taken to generate the response.</param>
public record GenerationResponse(
    string Answer,
    List<string> Sources,
    string Model,
    int TokensUsed,
    TimeSpan ResponseTime);
