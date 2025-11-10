using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Exceptions;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for PDF (.pdf) files using iText7.
/// </summary>
public class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : ITextExtractor
{
    /// <inheritdoc />
    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return await Task.Run(() =>
        {
            try
            {
                using var pdfReader = new PdfReader(fileStream);
                using var pdfDocument = new PdfDocument(pdfReader);

                var textBuilder = new StringBuilder();
                var pageCount = pdfDocument.GetNumberOfPages();

                for (var i = 1; i <= pageCount; i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new SimpleTextExtractionStrategy();
                    var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);
                    textBuilder.AppendLine(pageText);
                }

                var documentInfo = pdfDocument.GetDocumentInfo();
                var metadata = new Dictionary<string, object>
                {
                    ["OriginalFileName"] = fileName,
                    ["ExtractedAt"] = DateTime.UtcNow
                };

                // Add optional metadata if available
                var author = documentInfo.GetAuthor();
                if (!string.IsNullOrEmpty(author))
                    metadata["Author"] = author;

                var title = documentInfo.GetTitle();
                if (!string.IsNullOrEmpty(title))
                    metadata["Title"] = title;

                var producer = documentInfo.GetProducer();
                if (!string.IsNullOrEmpty(producer))
                    metadata["Producer"] = producer;

                var creationDate = documentInfo.GetMoreInfo(PdfName.CreationDate.GetValue());
                if (!string.IsNullOrEmpty(creationDate))
                    metadata["CreationDate"] = creationDate;

                logger.LogInformation(
                    "Text extracted successfully from PDF {FileName}, Pages: {PageCount}, Length: {TextLength}",
                    fileName,
                    pageCount,
                    textBuilder.Length);

                return new TextExtractionResult
                {
                    Text = textBuilder.ToString(),
                    Metadata = metadata
                };
            }
            catch (iText.IO.Exceptions.IOException ex)
            {
                logger.LogError(ex, "PDF is corrupted or encrypted: {FileName}", fileName);
                throw new TextExtractionException($"Failed to extract text from PDF: {fileName}. File may be corrupted or encrypted.", ex);
            }
            catch (iText.Kernel.Exceptions.PdfException ex)
            {
                logger.LogError(ex, "PDF processing error for {FileName}", fileName);
                throw new TextExtractionException($"Failed to extract text from PDF: {fileName}", ex);
            }
            catch (Exception ex) when (ex is not TextExtractionException)
            {
                logger.LogError(ex, "Text extraction failed for PDF {FileName}", fileName);
                throw new TextExtractionException($"Failed to extract text from PDF: {fileName}", ex);
            }
        }, cancellationToken);
    }
}
