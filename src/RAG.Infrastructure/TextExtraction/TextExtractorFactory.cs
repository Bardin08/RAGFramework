using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Composite text extractor that delegates to specific extractors based on file format.
/// </summary>
public class CompositeTextExtractor : ITextExtractor
{
    private readonly TxtTextExtractor _txtExtractor;
    private readonly DocxTextExtractor _docxExtractor;
    private readonly ILogger<CompositeTextExtractor> _logger;

    public CompositeTextExtractor(
        TxtTextExtractor txtExtractor,
        DocxTextExtractor docxExtractor,
        ILogger<CompositeTextExtractor> logger)
    {
        _txtExtractor = txtExtractor ?? throw new ArgumentNullException(nameof(txtExtractor));
        _docxExtractor = docxExtractor ?? throw new ArgumentNullException(nameof(docxExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extractor = GetExtractorForFile(fileName);

        if (extractor == null)
        {
            var extension = Path.GetExtension(fileName);
            throw new NotSupportedException(
                $"No text extractor found for file format: {extension}. " +
                $"Supported formats: .txt, .docx");
        }

        _logger.LogDebug(
            "Using {ExtractorType} for file {FileName}",
            extractor.GetType().Name, fileName);

        return await extractor.ExtractTextAsync(documentStream, fileName, cancellationToken);
    }

    /// <inheritdoc />
    public bool SupportsFormat(string fileName)
    {
        return GetExtractorForFile(fileName) != null;
    }

    /// <summary>
    /// Gets the appropriate extractor for the given file.
    /// </summary>
    private ITextExtractor? GetExtractorForFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".txt" => _txtExtractor,
            ".docx" => _docxExtractor,
            _ => null
        };
    }
}
