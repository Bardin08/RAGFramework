namespace RAG.Application.DTOs;

/// <summary>
/// Represents a processed user query with normalized text, detected language, and tokens.
/// </summary>
public record ProcessedQuery
{
    /// <summary>
    /// The original query text without any modifications.
    /// </summary>
    public required string OriginalText { get; init; }

    /// <summary>
    /// The normalized query text (trimmed, lowercased, extra spaces removed).
    /// </summary>
    public required string NormalizedText { get; init; }

    /// <summary>
    /// The detected language code ('en' for English or 'uk' for Ukrainian).
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// The tokenized query as a list of individual words.
    /// </summary>
    public required List<string> Tokens { get; init; }
}
