using RAG.Core.Domain.Enums;

namespace RAG.Application.Services;

/// <summary>
/// Fallback classifier using keyword-based heuristics for query classification.
/// Used when LLM classification is unavailable or times out.
/// </summary>
public class HeuristicClassifier
{
    private static readonly Dictionary<QueryType, string[]> KeywordPatterns = new()
    {
        [QueryType.ExplicitFact] = new[]
        {
            "what is", "who is", "when did", "when was", "where is", "where was",
            "define", "list", "name", "show me", "tell me about"
        },
        [QueryType.ImplicitFact] = new[]
        {
            "why", "how", "explain", "describe", "причина", "як", "чому",
            "what causes", "what leads to", "what makes"
        },
        [QueryType.InterpretableRationale] = new[]
        {
            "compare", "analyze", "evaluate", "difference", "порівняти", "аналізувати",
            "contrast", "versus", "vs", "pros and cons", "advantages"
        },
        [QueryType.HiddenRationale] = new[]
        {
            "should", "opinion", "think", "better", "recommend", "best",
            "prefer", "suggest", "would you", "do you think"
        }
    };

    /// <summary>
    /// Classifies a query using keyword matching heuristics.
    /// </summary>
    /// <param name="query">The query text to classify</param>
    /// <returns>The classified QueryType</returns>
    public QueryType Classify(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // Default to ExplicitFact for empty queries
            return QueryType.ExplicitFact;
        }

        var normalizedQuery = query.ToLowerInvariant();

        // Score each type based on keyword matches
        var scores = new Dictionary<QueryType, int>();
        foreach (var (type, keywords) in KeywordPatterns)
        {
            scores[type] = keywords.Count(kw => normalizedQuery.Contains(kw));
        }

        // Return type with highest score
        var maxScore = scores.Values.Max();
        if (maxScore > 0)
        {
            return scores.First(s => s.Value == maxScore).Key;
        }

        // Default to ExplicitFact if no matches found
        return QueryType.ExplicitFact;
    }
}
