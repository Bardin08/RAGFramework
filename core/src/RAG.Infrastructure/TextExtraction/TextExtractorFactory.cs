using Microsoft.Extensions.DependencyInjection;
using RAG.Application.Interfaces;
using RAG.Core.Exceptions;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Factory implementation for creating text extractors based on file extension.
/// Uses lazy instantiation to only create extractors when needed.
/// </summary>
public class TextExtractorFactory(IServiceProvider serviceProvider) : ITextExtractorFactory
{
    /// <inheritdoc />
    public ITextExtractor GetExtractor(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".txt" => serviceProvider.GetRequiredService<TxtTextExtractor>(),
            ".pdf" => serviceProvider.GetRequiredService<PdfTextExtractor>(),
            ".docx" => serviceProvider.GetRequiredService<DocxTextExtractor>(),
            _ => throw new UnsupportedFileTypeException($"File type not supported: {extension}")
        };
    }
}
