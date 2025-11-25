using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using System.Text;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for PDF (.pdf) files using Docnet.Core (PDFium).
/// </summary>
public class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : ITextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        if (!SupportsFormat(fileName))
        {
            throw new NotSupportedException($"File format not supported: {fileName}");
        }

        try
        {
            byte[] pdfBytes;
            if (documentStream is MemoryStream ms)
            {
                pdfBytes = ms.ToArray();
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await documentStream.CopyToAsync(memoryStream, cancellationToken);
                pdfBytes = memoryStream.ToArray();
            }

            var text = await Task.Run(() => ExtractTextFromPdf(pdfBytes), cancellationToken);

            _logger.LogDebug(
                "Successfully extracted {Length} characters from PDF document {FileName}",
                text.Length, fileName);

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from {FileName}", fileName);
            throw;
        }
    }

    /// <inheritdoc />
    public bool SupportsFormat(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    /// <summary>
    /// Extracts text from a PDF document using Docnet/PDFium.
    /// </summary>
    private string ExtractTextFromPdf(byte[] pdfBytes)
    {
        var textBuilder = new StringBuilder();

        using (var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1080, 1920)))
        {
            for (var pageIndex = 0; pageIndex < docReader.GetPageCount(); pageIndex++)
            {
                try
                {
                    using var pageReader = docReader.GetPageReader(pageIndex);
                    var pageText = pageReader.GetText();

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to extract text from page {PageIndex}. Skipping page.",
                        pageIndex);
                }
            }
        }

        return textBuilder.ToString().Trim();
    }
}
