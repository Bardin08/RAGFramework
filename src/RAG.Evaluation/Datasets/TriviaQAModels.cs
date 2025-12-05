using System.Text.Json.Serialization;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Represents a TriviaQA dataset entry.
/// Based on the TriviaQA RC dataset format from https://nlp.cs.washington.edu/triviaqa/
/// </summary>
public class TriviaQAEntry
{
    /// <summary>
    /// Unique identifier for this question.
    /// </summary>
    [JsonPropertyName("QuestionId")]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>
    /// The question text.
    /// </summary>
    [JsonPropertyName("Question")]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// The primary answer.
    /// </summary>
    [JsonPropertyName("Answer")]
    public TriviaQAAnswer? Answer { get; set; }

    /// <summary>
    /// Source of the question (e.g., "Web", "Wikipedia").
    /// </summary>
    [JsonPropertyName("QuestionSource")]
    public string? QuestionSource { get; set; }

    /// <summary>
    /// Wikipedia evidence documents.
    /// </summary>
    [JsonPropertyName("EntityPages")]
    public List<TriviaQAEntityPage>? EntityPages { get; set; }

    /// <summary>
    /// Web search evidence documents.
    /// </summary>
    [JsonPropertyName("SearchResults")]
    public List<TriviaQASearchResult>? SearchResults { get; set; }
}

/// <summary>
/// Represents an answer with aliases.
/// </summary>
public class TriviaQAAnswer
{
    /// <summary>
    /// The primary/normalized answer value.
    /// </summary>
    [JsonPropertyName("Value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Alternative valid answers/aliases.
    /// </summary>
    [JsonPropertyName("Aliases")]
    public List<string>? Aliases { get; set; }

    /// <summary>
    /// Normalized aliases (lowercased, trimmed).
    /// </summary>
    [JsonPropertyName("NormalizedAliases")]
    public List<string>? NormalizedAliases { get; set; }

    /// <summary>
    /// Gets all valid answers including the primary value and all aliases.
    /// </summary>
    public IEnumerable<string> GetAllValidAnswers()
    {
        yield return Value;

        if (Aliases != null)
        {
            foreach (var alias in Aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                    yield return alias;
            }
        }
    }

    /// <summary>
    /// Gets all normalized valid answers.
    /// </summary>
    public IEnumerable<string> GetAllNormalizedAnswers()
    {
        yield return Value.ToLowerInvariant().Trim();

        if (NormalizedAliases != null)
        {
            foreach (var normalized in NormalizedAliases)
            {
                if (!string.IsNullOrWhiteSpace(normalized))
                    yield return normalized;
            }
        }
    }
}

/// <summary>
/// Represents a Wikipedia entity page with evidence.
/// </summary>
public class TriviaQAEntityPage
{
    /// <summary>
    /// Document/entity identifier.
    /// </summary>
    [JsonPropertyName("DocSource")]
    public string? DocSource { get; set; }

    /// <summary>
    /// The title of the Wikipedia page.
    /// </summary>
    [JsonPropertyName("Title")]
    public string? Title { get; set; }

    /// <summary>
    /// Wikipedia page filename.
    /// </summary>
    [JsonPropertyName("Filename")]
    public string? Filename { get; set; }

    /// <summary>
    /// Full text content of the Wikipedia page.
    /// </summary>
    [JsonPropertyName("WikipediaText")]
    public string? WikipediaText { get; set; }
}

/// <summary>
/// Represents a web search result with evidence.
/// </summary>
public class TriviaQASearchResult
{
    /// <summary>
    /// Document/search result identifier.
    /// </summary>
    [JsonPropertyName("DocSource")]
    public string? DocSource { get; set; }

    /// <summary>
    /// Title of the search result.
    /// </summary>
    [JsonPropertyName("Title")]
    public string? Title { get; set; }

    /// <summary>
    /// URL of the search result.
    /// </summary>
    [JsonPropertyName("Url")]
    public string? Url { get; set; }

    /// <summary>
    /// Search result filename (for cached content).
    /// </summary>
    [JsonPropertyName("Filename")]
    public string? Filename { get; set; }

    /// <summary>
    /// Snippet/description from search results.
    /// </summary>
    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    /// <summary>
    /// Search rank/position.
    /// </summary>
    [JsonPropertyName("Rank")]
    public int? Rank { get; set; }

    /// <summary>
    /// Full text content of the web page.
    /// </summary>
    [JsonPropertyName("PageText")]
    public string? PageText { get; set; }
}

/// <summary>
/// Represents a processed TriviaQA document for indexing.
/// </summary>
public class TriviaQADocument
{
    /// <summary>
    /// Unique document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Question ID this document is evidence for.
    /// </summary>
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>
    /// Document title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full document text content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Source type: "Wikipedia" or "Web".
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// URL for web sources.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
