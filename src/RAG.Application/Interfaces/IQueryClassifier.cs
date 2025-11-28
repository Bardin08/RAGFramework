using RAG.Core.Domain.Enums;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for classifying user queries into predefined QueryType categories.
/// Uses LLM-based classification with heuristic fallback for reliability.
/// </summary>
public interface IQueryClassifier
{
    /// <summary>
    /// Classifies a user query into one of the QueryType categories.
    /// </summary>
    /// <param name="query">The user query text to classify</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The classified QueryType</returns>
    Task<QueryType> ClassifyQueryAsync(string query, CancellationToken cancellationToken = default);
}
