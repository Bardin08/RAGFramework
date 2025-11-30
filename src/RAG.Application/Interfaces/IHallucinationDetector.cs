using RAG.Application.Models;

namespace RAG.Application.Interfaces;

/// <summary>
/// Detects hallucinations in LLM-generated responses through multiple validation mechanisms.
/// </summary>
public interface IHallucinationDetector
{
    /// <summary>
    /// Detects potential hallucinations in a generated response.
    /// </summary>
    /// <param name="response">The LLM-generated response text.</param>
    /// <param name="context">The retrieved context used for generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hallucination detection result with confidence scores.</returns>
    Task<HallucinationResult> DetectAsync(
        string response,
        string context,
        CancellationToken cancellationToken = default);
}
