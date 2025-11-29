using RAG.Core.Domain;

namespace RAG.Application.Services;

/// <summary>
/// Interface for assembling formatted context from retrieval results for LLM input.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Assembles formatted context from retrieval results, respecting token limits.
    /// </summary>
    /// <param name="results">Retrieval results to assemble context from.</param>
    /// <param name="maxTokens">Maximum token limit (optional, uses config default if null).</param>
    /// <returns>Formatted context string with source references.</returns>
    string AssembleContext(List<RetrievalResult> results, int? maxTokens = null);
}
