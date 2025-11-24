using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.TextExtraction;

/// <summary>
/// Text extractor for plain text (.txt) files.
/// </summary>
public class TxtTextExtractor : ITextExtractor
{
    private readonly ILogger<TxtTextExtractor> _logger;

    public TxtTextExtractor(ILogger<TxtTextExtractor> logger)
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
            using var reader = new StreamReader(documentStream, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully extracted {Length} characters from text file {FileName}",
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
        return extension == ".txt";
    }
}
