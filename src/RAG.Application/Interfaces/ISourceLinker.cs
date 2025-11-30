using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Links source citations in LLM responses to actual retrieval results.
/// Extracts citation numbers like [Source 1] and maps them to source metadata.
/// </summary>
public interface ISourceLinker
{
    /// <summary>
    /// Extracts source citations from response and links them to retrieval results.
    /// </summary>
    /// <param name="response">The LLM-generated response containing source citations.</param>
    /// <param name="retrievalResults">The retrieval results used to generate the response.</param>
    /// <returns>List of source references with metadata extracted from retrieval results.</returns>
    List<SourceReference> LinkSources(string response, List<RetrievalResult> retrievalResults);
}
