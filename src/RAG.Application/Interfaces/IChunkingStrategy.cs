using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a strategy for chunking text documents into smaller segments for processing and indexing.
/// </summary>
/// <remarks>
/// Implementations of this interface should handle text segmentation using various algorithms
/// such as sliding window, semantic chunking, or sentence-based splitting.
/// Each chunk maintains position tracking (StartIndex, EndIndex) to preserve document context.
/// </remarks>
public interface IChunkingStrategy
{
    /// <summary>
    /// Chunks the provided text into smaller segments asynchronously.
    /// </summary>
    /// <param name="text">The text content to be chunked. Must not be null or empty.</param>
    /// <param name="documentId">The unique identifier of the source document. Used to link chunks back to their parent document.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of document chunks.</returns>
    /// <exception cref="ArgumentException">Thrown when text is null or empty, or documentId is empty.</exception>
    Task<List<DocumentChunk>> ChunkAsync(string text, Guid documentId, CancellationToken cancellationToken = default);
}
