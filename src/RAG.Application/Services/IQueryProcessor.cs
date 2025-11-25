using RAG.Application.DTOs;

namespace RAG.Application.Services;

/// <summary>
/// Interface for processing and normalizing user queries.
/// </summary>
public interface IQueryProcessor
{
    /// <summary>
    /// Processes a query by normalizing text, detecting language, and tokenizing.
    /// </summary>
    /// <param name="queryText">The raw query text from the user.</param>
    /// <returns>A ProcessedQuery with normalized text, detected language, and tokens.</returns>
    ProcessedQuery ProcessQuery(string queryText);
}
