using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Exceptions;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for Word (.docx) files using DocumentFormat.OpenXml.
/// </summary>
public class DocxTextExtractor(ILogger<DocxTextExtractor> logger) : ITextExtractor
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
                using var doc = WordprocessingDocument.Open(fileStream, false);
                var body = doc.MainDocumentPart?.Document?.Body;

                if (body == null)
                {
                    logger.LogWarning("DOCX file {FileName} has no body content, returning empty result", fileName);
                    return new TextExtractionResult
                    {
                        Text = string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["OriginalFileName"] = fileName,
                            ["ExtractedAt"] = DateTime.UtcNow
                        }
                    };
                }

                var textBuilder = new StringBuilder();
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    textBuilder.AppendLine(paragraph.InnerText);
                }

                var coreProps = doc.PackageProperties;
                var metadata = new Dictionary<string, object>
                {
                    ["OriginalFileName"] = fileName,
                    ["ExtractedAt"] = DateTime.UtcNow
                };

                // Add optional metadata if available
                if (!string.IsNullOrEmpty(coreProps.Creator))
                    metadata["Author"] = coreProps.Creator;

                if (!string.IsNullOrEmpty(coreProps.Title))
                    metadata["Title"] = coreProps.Title;

                if (coreProps.Created.HasValue)
                    metadata["Created"] = coreProps.Created.Value;

                if (coreProps.Modified.HasValue)
                    metadata["Modified"] = coreProps.Modified.Value;

                logger.LogInformation(
                    "Text extracted successfully from DOCX {FileName}, Length: {TextLength}",
                    fileName,
                    textBuilder.Length);

                return new TextExtractionResult
                {
                    Text = textBuilder.ToString(),
                    Metadata = metadata
                };
            }
            catch (OpenXmlPackageException ex)
            {
                logger.LogError(ex, "DOCX file is corrupted or invalid: {FileName}", fileName);
                throw new TextExtractionException($"Failed to extract text from DOCX: {fileName}. File may be corrupted.", ex);
            }
            catch (Exception ex) when (ex is not TextExtractionException)
            {
                logger.LogError(ex, "Text extraction failed for DOCX {FileName}", fileName);
                throw new TextExtractionException($"Failed to extract text from DOCX: {fileName}", ex);
            }
        }, cancellationToken);
    }
}
