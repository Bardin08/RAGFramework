using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using System.Text;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for Word (.docx) files using DocumentFormat.OpenXml.
/// </summary>
public class DocxTextExtractor : ITextExtractor
{
    private readonly ILogger<DocxTextExtractor> _logger;

    public DocxTextExtractor(ILogger<DocxTextExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (documentStream == null)
        {
            throw new ArgumentNullException(nameof(documentStream));
        }

        if (!SupportsFormat(fileName))
        {
            throw new NotSupportedException($"File format not supported: {fileName}");
        }

        try
        {
            // DocumentFormat.OpenXml requires a seekable stream
            // Copy to MemoryStream if the original stream is not seekable
            Stream workingStream = documentStream;
            if (!documentStream.CanSeek)
            {
                var memoryStream = new MemoryStream();
                await documentStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                workingStream = memoryStream;
            }
            else
            {
                documentStream.Position = 0;
            }

            var text = ExtractTextFromDocx(workingStream);

            _logger.LogDebug(
                "Successfully extracted {Length} characters from Word document {FileName}",
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
        return extension == ".docx";
    }

    /// <summary>
    /// Extracts text from a Word document stream.
    /// </summary>
    private string ExtractTextFromDocx(Stream stream)
    {
        var textBuilder = new StringBuilder();

        using (var wordDocument = WordprocessingDocument.Open(stream, false))
        {
            if (wordDocument.MainDocumentPart == null)
            {
                throw new InvalidOperationException("Document does not contain a main document part");
            }

            var body = wordDocument.MainDocumentPart.Document.Body;

            if (body == null)
            {
                return string.Empty;
            }

            // Extract text from all paragraphs
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                var paragraphText = GetParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    textBuilder.AppendLine(paragraphText);
                }
            }

            // Extract text from tables
            foreach (var table in body.Descendants<Table>())
            {
                foreach (var row in table.Descendants<TableRow>())
                {
                    var rowTexts = new List<string>();
                    foreach (var cell in row.Descendants<TableCell>())
                    {
                        var cellText = string.Join(" ", cell.Descendants<Paragraph>()
                            .Select(GetParagraphText)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));

                        if (!string.IsNullOrWhiteSpace(cellText))
                        {
                            rowTexts.Add(cellText);
                        }
                    }

                    if (rowTexts.Count > 0)
                    {
                        textBuilder.AppendLine(string.Join(" | ", rowTexts));
                    }
                }
            }
        }

        return textBuilder.ToString().Trim();
    }

    /// <summary>
    /// Gets text content from a paragraph, including all text runs.
    /// </summary>
    private static string GetParagraphText(Paragraph paragraph)
    {
        var textBuilder = new StringBuilder();

        foreach (var text in paragraph.Descendants<Text>())
        {
            textBuilder.Append(text.Text);
        }

        return textBuilder.ToString();
    }
}
