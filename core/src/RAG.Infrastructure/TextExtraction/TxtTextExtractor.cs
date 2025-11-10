using System.Text;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Exceptions;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for plain text (.txt) files.
/// </summary>
public class TxtTextExtractor(ILogger<TxtTextExtractor> logger) : ITextExtractor
{
    /// <inheritdoc />
    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);

            // Reset stream position for potential subsequent operations
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var metadata = new Dictionary<string, object>
            {
                ["OriginalFileName"] = fileName,
                ["ExtractedAt"] = DateTime.UtcNow
            };

            logger.LogInformation(
                "Text extracted successfully from {FileName}, Length: {TextLength}",
                fileName,
                text.Length);

            return new TextExtractionResult
            {
                Text = text,
                Metadata = metadata
            };
        }
        catch (Exception ex) when (ex is not TextExtractionException)
        {
            logger.LogError(ex, "Text extraction failed for {FileName}", fileName);
            throw new TextExtractionException($"Failed to extract text from {fileName}", ex);
        }
    }
}
