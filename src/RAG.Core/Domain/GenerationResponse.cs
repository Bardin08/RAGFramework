namespace RAG.Core.Domain;

/// <summary>
/// Represents a response from the LLM generation service.
/// </summary>
/// <param name="Answer">The generated answer text.</param>
/// <param name="Model">The LLM model name/version used for generation.</param>
/// <param name="TokensUsed">The total number of tokens consumed in generation.</param>
/// <param name="ResponseTime">The duration of the generation process.</param>
/// <param name="Sources">List of source citations from context used in the answer.</param>
public record GenerationResponse(
    string Answer,
    string Model,
    int TokensUsed,
    TimeSpan ResponseTime,
    List<SourceReference> Sources);
