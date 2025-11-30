namespace RAG.Application.Models;

/// <summary>
/// Result of response validation containing validation status and issues found.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Indicates if the response passed all validation checks.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation issues found (empty if IsValid is true).
    /// </summary>
    public List<string> Issues { get; init; } = new();

    /// <summary>
    /// Relevance score between query and response (0.0 to 1.0).
    /// </summary>
    public decimal RelevanceScore { get; init; }

    /// <summary>
    /// Number of source citations found in the response.
    /// </summary>
    public int CitationCount { get; init; }
}
