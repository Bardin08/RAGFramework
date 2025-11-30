using RAG.Application.Models;
using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Validates LLM-generated responses for quality, relevance, and proper source citations.
/// </summary>
public interface IResponseValidator
{
    /// <summary>
    /// Validates a generated response against the original query and retrieval results.
    /// </summary>
    /// <param name="response">The LLM-generated response text.</param>
    /// <param name="query">The original user query.</param>
    /// <param name="retrievalResults">The retrieval results used to generate the response.</param>
    /// <returns>Validation result containing issues and metrics.</returns>
    ValidationResult ValidateResponse(
        string response,
        string query,
        List<RetrievalResult> retrievalResults);
}
