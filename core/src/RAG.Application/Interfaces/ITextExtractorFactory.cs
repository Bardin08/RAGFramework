using RAG.Core.Exceptions;

namespace RAG.Application.Interfaces;

/// <summary>
/// Factory for creating text extractors based on file type.
/// </summary>
public interface ITextExtractorFactory
{
    /// <summary>
    /// Gets the appropriate text extractor for the specified file name.
    /// </summary>
    /// <param name="fileName">The file name (used to determine file extension).</param>
    /// <returns>An <see cref="ITextExtractor"/> implementation for the file type.</returns>
    /// <exception cref="UnsupportedFileTypeException">Thrown when the file type is not supported.</exception>
    ITextExtractor GetExtractor(string fileName);
}
