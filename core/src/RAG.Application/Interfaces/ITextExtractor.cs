using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Interface for extracting text from various document formats.
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text and metadata from a document stream.
    /// </summary>
    /// <param name="fileStream">The stream containing the document content.</param>
    /// <param name="fileName">The original file name (used for extension detection and metadata).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="TextExtractionResult"/> containing extracted text and metadata.</returns>
    /// <exception cref="TextExtractionException">Thrown when text extraction fails.</exception>
    Task<TextExtractionResult> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
