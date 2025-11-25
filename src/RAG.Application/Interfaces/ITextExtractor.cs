namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a contract for extracting text from documents of various formats.
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text content from a document stream.
    /// </summary>
    /// <param name="documentStream">The document stream to extract text from.</param>
    /// <param name="fileName">The name of the file (used to determine format).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted text content.</returns>
    Task<string> ExtractTextAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this extractor supports the given file format.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>True if the format is supported, false otherwise.</returns>
    bool SupportsFormat(string fileName);
}
