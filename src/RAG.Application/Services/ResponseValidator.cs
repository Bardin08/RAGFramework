using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Application.Models;
using RAG.Core.Domain;
using System.Text.RegularExpressions;

namespace RAG.Application.Services;

/// <summary>
/// Validates LLM responses for quality, relevance, and proper source citations.
/// Non-blocking validator that logs warnings but doesn't prevent response delivery.
/// </summary>
public class ResponseValidator : IResponseValidator
{
    private readonly ILogger<ResponseValidator> _logger;
    private const decimal DefaultRelevanceThreshold = 0.3m;

    // Regex to match source citations like [Source 1], [Source 2], etc.
    private static readonly Regex SourceCitationRegex = new(@"\[Source\s+\d+\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Common English stop words to exclude from keyword matching
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from",
        "has", "he", "in", "is", "it", "its", "of", "on", "that", "the",
        "to", "was", "will", "with", "what", "when", "where", "who", "which",
        "this", "these", "those", "can", "could", "should", "would", "do", "does"
    };

    public ResponseValidator(ILogger<ResponseValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public ValidationResult ValidateResponse(
        string response,
        string query,
        List<RetrievalResult> retrievalResults)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (retrievalResults == null) throw new ArgumentNullException(nameof(retrievalResults));

        var issues = new List<string>();
        var citationCount = 0;
        var relevanceScore = 0m;

        // Validation 1: Check if response is empty
        if (string.IsNullOrWhiteSpace(response))
        {
            issues.Add("Response is empty or whitespace");
            _logger.LogWarning("Validation failed: Empty response for query '{Query}'", query);

            return new ValidationResult
            {
                IsValid = false,
                Issues = issues,
                RelevanceScore = 0m,
                CitationCount = 0
            };
        }

        // Validation 2: Check for source citations
        citationCount = CountSourceCitations(response);
        if (citationCount == 0)
        {
            issues.Add("Response missing source citations in format [Source N]");
            _logger.LogWarning(
                "Validation warning: No source citations found in response for query '{Query}'",
                query);
        }

        // Validation 3: Check relevance to query using keyword matching
        relevanceScore = CalculateRelevanceScore(response, query);
        if (relevanceScore < DefaultRelevanceThreshold)
        {
            issues.Add($"Response may not be relevant to query (relevance score: {relevanceScore:F2})");
            _logger.LogWarning(
                "Validation warning: Low relevance score {RelevanceScore:F2} for query '{Query}'",
                relevanceScore,
                query);
        }

        var isValid = issues.Count == 0;

        _logger.LogInformation(
            "Response validation completed: IsValid={IsValid}, CitationCount={CitationCount}, RelevanceScore={RelevanceScore:F2}, Issues={IssueCount}",
            isValid,
            citationCount,
            relevanceScore,
            issues.Count);

        return new ValidationResult
        {
            IsValid = isValid,
            Issues = issues,
            RelevanceScore = relevanceScore,
            CitationCount = citationCount
        };
    }

    /// <summary>
    /// Counts the number of source citations in the response.
    /// </summary>
    private int CountSourceCitations(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return 0;

        var matches = SourceCitationRegex.Matches(response);
        return matches.Count;
    }

    /// <summary>
    /// Calculates relevance score between response and query using keyword overlap heuristic.
    /// </summary>
    /// <returns>Relevance score between 0.0 and 1.0</returns>
    private decimal CalculateRelevanceScore(string response, string query)
    {
        if (string.IsNullOrWhiteSpace(response) || string.IsNullOrWhiteSpace(query))
            return 0m;

        // Extract keywords from query (remove stop words, normalize to lowercase)
        var queryKeywords = ExtractKeywords(query);

        if (queryKeywords.Count == 0)
            return 0m;

        // Check how many query keywords appear in the response
        var responseLower = response.ToLowerInvariant();
        var matchedKeywords = queryKeywords.Count(keyword => responseLower.Contains(keyword));

        // Calculate overlap percentage
        var relevanceScore = (decimal)matchedKeywords / queryKeywords.Count;

        _logger.LogDebug(
            "Relevance calculation: {MatchedKeywords}/{TotalKeywords} query keywords found in response. Score: {RelevanceScore:F2}",
            matchedKeywords,
            queryKeywords.Count,
            relevanceScore);

        return relevanceScore;
    }

    /// <summary>
    /// Extracts meaningful keywords from text by removing stop words and normalizing.
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Split by whitespace and punctuation, convert to lowercase, remove stop words
        var words = Regex.Split(text, @"\W+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.ToLowerInvariant())
            .Where(w => !StopWords.Contains(w) && w.Length > 2) // Also filter out very short words
            .Distinct()
            .ToList();

        return words;
    }
}
