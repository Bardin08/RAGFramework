using RAG.Core.Domain.Enums;

namespace RAG.Core.Domain;

/// <summary>
/// Represents a user query in the RAG system.
/// </summary>
public record Query
{
    /// <summary>
    /// Unique identifier for the query.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The text of the user's query.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// The language code of the query ('en' or 'uk').
    /// </summary>
    public string Language { get; init; }

    /// <summary>
    /// The timestamp when the query was submitted.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// The type of reasoning required for this query.
    /// </summary>
    public QueryType QueryType { get; init; }

    /// <summary>
    /// Creates a new Query instance with validation.
    /// </summary>
    public Query(Guid id, string text, string language, DateTime timestamp, QueryType queryType)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Query ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Query text cannot be empty", nameof(text));

        if (language != "en" && language != "uk")
            throw new ArgumentException("Language must be 'en' or 'uk'", nameof(language));

        Id = id;
        Text = text;
        Language = language;
        Timestamp = timestamp;
        QueryType = queryType;
    }
}
