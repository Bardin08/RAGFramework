using System.Text.RegularExpressions;
using RAG.Application.DTOs;

namespace RAG.Application.Services;

/// <summary>
/// Processes and normalizes user queries for retrieval strategies.
/// </summary>
public class QueryProcessor : IQueryProcessor
{
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Processes a query by normalizing text, detecting language, and tokenizing.
    /// </summary>
    /// <param name="queryText">The raw query text from the user.</param>
    /// <returns>A ProcessedQuery with normalized text, detected language, and tokens.</returns>
    /// <exception cref="ArgumentNullException">Thrown when queryText is null.</exception>
    public ProcessedQuery ProcessQuery(string queryText)
    {
        if (queryText == null)
            throw new ArgumentNullException(nameof(queryText), "Query text cannot be null");

        var originalText = queryText;
        var normalizedText = NormalizeText(queryText);
        var language = DetectLanguage(normalizedText);
        var tokens = Tokenize(normalizedText);

        return new ProcessedQuery
        {
            OriginalText = originalText,
            NormalizedText = normalizedText,
            Language = language,
            Tokens = tokens
        };
    }

    /// <summary>
    /// Normalizes query text by trimming, lowercasing, and removing extra spaces.
    /// </summary>
    private static string NormalizeText(string text)
    {
        // Trim whitespace from start and end
        var trimmed = text.Trim();

        // Convert to lowercase for case-insensitive matching
        var lowercased = trimmed.ToLowerInvariant();

        // Remove multiple consecutive spaces (replace with single space)
        var normalized = MultipleSpacesRegex.Replace(lowercased, " ");

        return normalized;
    }

    /// <summary>
    /// Detects the language of the query using a simple heuristic.
    /// Returns "uk" if Cyrillic characters are detected, otherwise "en".
    /// </summary>
    private static string DetectLanguage(string text)
    {
        // Check for Cyrillic characters (Ukrainian range: U+0400 to U+04FF)
        if (text.Any(c => c >= '\u0400' && c <= '\u04FF'))
            return "uk";

        // Default to English
        return "en";
    }

    /// <summary>
    /// Tokenizes the query by splitting on whitespace.
    /// </summary>
    private static List<string> Tokenize(string text)
    {
        // Split on whitespace and remove empty entries
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
