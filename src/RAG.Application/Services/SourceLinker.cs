using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using System.Text.RegularExpressions;

namespace RAG.Application.Services;

/// <summary>
/// Links source citations in LLM responses to retrieval results.
/// Extracts [Source N] citations and maps them to source metadata.
/// </summary>
public class SourceLinker : ISourceLinker
{
    private readonly ILogger<SourceLinker> _logger;

    // Regex to extract source citation numbers like [Source 1], [Source 2], etc.
    private static readonly Regex SourceCitationRegex = new(
        @"\[Source\s+(\d+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SourceLinker(ILogger<SourceLinker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public List<SourceReference> LinkSources(string response, List<RetrievalResult> retrievalResults)
    {
        if (retrievalResults == null) throw new ArgumentNullException(nameof(retrievalResults));

        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("Empty response provided for source linking");
            return new List<SourceReference>();
        }

        // Extract all unique source citation numbers from response
        var citedSourceNumbers = ExtractCitedSourceNumbers(response);

        if (citedSourceNumbers.Count == 0)
        {
            _logger.LogWarning("No source citations found in response");
            return new List<SourceReference>();
        }

        // Map citation numbers to retrieval results (1-based indexing)
        var linkedSources = new List<SourceReference>();

        foreach (var sourceNumber in citedSourceNumbers)
        {
            // Convert 1-based citation to 0-based index
            var index = sourceNumber - 1;

            if (index < 0 || index >= retrievalResults.Count)
            {
                _logger.LogWarning(
                    "Source citation [Source {SourceNumber}] out of range. Available results: {Count}",
                    sourceNumber,
                    retrievalResults.Count);
                continue;
            }

            var retrievalResult = retrievalResults[index];

            // Create SourceReference from RetrievalResult
            var sourceReference = new SourceReference(
                SourceId: retrievalResult.DocumentId,
                Title: retrievalResult.Source,
                Excerpt: TruncateExcerpt(retrievalResult.Text, maxLength: 200),
                Score: retrievalResult.Score
            );

            linkedSources.Add(sourceReference);

            _logger.LogDebug(
                "Linked [Source {SourceNumber}] to document {DocumentId} ('{Title}')",
                sourceNumber,
                retrievalResult.DocumentId,
                retrievalResult.Source);
        }

        _logger.LogInformation(
            "Source linking completed: {LinkedCount}/{CitedCount} sources linked successfully",
            linkedSources.Count,
            citedSourceNumbers.Count);

        return linkedSources;
    }

    /// <summary>
    /// Extracts unique source citation numbers from the response text.
    /// </summary>
    /// <returns>Sorted list of unique source numbers (1-based).</returns>
    private List<int> ExtractCitedSourceNumbers(string response)
    {
        var matches = SourceCitationRegex.Matches(response);
        var sourceNumbers = new HashSet<int>();

        foreach (Match match in matches)
        {
            if (match.Success && int.TryParse(match.Groups[1].Value, out var sourceNumber))
            {
                sourceNumbers.Add(sourceNumber);
            }
        }

        return sourceNumbers.OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Truncates excerpt to a maximum length with ellipsis.
    /// </summary>
    private string TruncateExcerpt(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}
