using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Generates ground truth files from Natural Questions dataset entries.
/// </summary>
public class NaturalQuestionsGroundTruthGenerator
{
    private readonly ILogger<NaturalQuestionsGroundTruthGenerator> _logger;
    private readonly HtmlToTextConverter _htmlConverter;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NaturalQuestionsGroundTruthGenerator(
        ILogger<NaturalQuestionsGroundTruthGenerator> logger,
        HtmlToTextConverter htmlConverter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _htmlConverter = htmlConverter ?? throw new ArgumentNullException(nameof(htmlConverter));
    }

    /// <summary>
    /// Generates ground truth JSON file from Natural Questions entries.
    /// </summary>
    /// <param name="entries">The Natural Questions entries to convert.</param>
    /// <param name="outputPath">Path to save the ground truth JSON file.</param>
    /// <param name="includeOnlyWithShortAnswers">If true, only includes entries that have short answers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task GenerateGroundTruthAsync(
        List<NaturalQuestionsEntry> entries,
        string outputPath,
        bool includeOnlyWithShortAnswers = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating ground truth file from {EntryCount} Natural Questions entries to {OutputPath}",
            entries.Count, outputPath);

        var groundTruthEntries = new List<GroundTruthJsonEntry>();
        var skippedCount = 0;

        foreach (var entry in entries)
        {
            // Skip entries without short answers if configured
            if (includeOnlyWithShortAnswers && !entry.HasShortAnswer)
            {
                skippedCount++;
                continue;
            }

            // Get the expected answer (prefer short answer, fallback to long answer)
            var expectedAnswer = GetExpectedAnswer(entry);
            if (string.IsNullOrWhiteSpace(expectedAnswer))
            {
                skippedCount++;
                continue;
            }

            // Create document ID from URL or entry ID
            var documentId = CreateDocumentId(entry);

            var groundTruthEntry = new GroundTruthJsonEntry
            {
                Query = entry.QuestionText,
                ExpectedAnswer = expectedAnswer,
                RelevantDocuments = [documentId],
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = "natural-questions",
                    ["documentUrl"] = entry.DocumentUrl,
                    ["documentTitle"] = entry.DocumentTitle,
                    ["hasShortAnswer"] = entry.HasShortAnswer,
                    ["shortAnswerCount"] = entry.ShortAnswers.Count
                }
            };

            groundTruthEntries.Add(groundTruthEntry);
        }

        _logger.LogInformation(
            "Generated {GroundTruthCount} ground truth entries. Skipped: {SkippedCount}",
            groundTruthEntries.Count, skippedCount);

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write to JSON file
        var json = JsonSerializer.Serialize(groundTruthEntries, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        _logger.LogInformation("Ground truth file written to: {OutputPath}", outputPath);
    }

    /// <summary>
    /// Generates a document collection from Natural Questions entries.
    /// </summary>
    /// <param name="entries">The Natural Questions entries to convert.</param>
    /// <param name="outputDirectory">Directory to save document files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapping of document IDs to file paths.</returns>
    public async Task<Dictionary<string, string>> GenerateDocumentsAsync(
        List<NaturalQuestionsEntry> entries,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating documents from {EntryCount} Natural Questions entries to {OutputDirectory}",
            entries.Count, outputDirectory);

        // Ensure output directory exists
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var documentMapping = new Dictionary<string, string>();
        var processedUrls = new HashSet<string>();

        foreach (var entry in entries)
        {
            // Skip duplicate documents (same URL)
            if (!string.IsNullOrWhiteSpace(entry.DocumentUrl) &&
                processedUrls.Contains(entry.DocumentUrl))
            {
                continue;
            }

            var documentId = CreateDocumentId(entry);
            var cleanText = _htmlConverter.Convert(entry.DocumentHtml);

            if (string.IsNullOrWhiteSpace(cleanText))
            {
                _logger.LogWarning(
                    "Skipping entry {EntryId} - no clean text extracted from HTML",
                    entry.Id);
                continue;
            }

            var document = new NaturalQuestionsDocument
            {
                Id = documentId,
                Title = entry.DocumentTitle,
                Url = entry.DocumentUrl,
                Content = cleanText,
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = "natural-questions",
                    ["originalUrl"] = entry.DocumentUrl,
                    ["characterCount"] = cleanText.Length
                }
            };

            // Save document to JSON file
            var fileName = $"{documentId}.json";
            var filePath = Path.Combine(outputDirectory, fileName);

            var json = JsonSerializer.Serialize(document, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            documentMapping[documentId] = filePath;

            if (!string.IsNullOrWhiteSpace(entry.DocumentUrl))
            {
                processedUrls.Add(entry.DocumentUrl);
            }
        }

        _logger.LogInformation(
            "Generated {DocumentCount} unique documents in {OutputDirectory}",
            documentMapping.Count, outputDirectory);

        return documentMapping;
    }

    /// <summary>
    /// Gets the expected answer from an NQ entry.
    /// Prefers short answers, falls back to long answer.
    /// </summary>
    private static string GetExpectedAnswer(NaturalQuestionsEntry entry)
    {
        // Prefer short answers
        if (entry.ShortAnswers.Count > 0)
        {
            // Join multiple short answers with " OR "
            var shortAnswerTexts = entry.ShortAnswers
                .Select(sa => sa.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (shortAnswerTexts.Count > 0)
            {
                return string.Join(" OR ", shortAnswerTexts);
            }
        }

        // Fallback to long answer
        if (entry.LongAnswer != null && !string.IsNullOrWhiteSpace(entry.LongAnswer.Text))
        {
            return entry.LongAnswer.Text.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a unique document ID from an NQ entry.
    /// </summary>
    private static string CreateDocumentId(NaturalQuestionsEntry entry)
    {
        // Try to extract a clean ID from the Wikipedia URL
        if (!string.IsNullOrWhiteSpace(entry.DocumentUrl))
        {
            var url = entry.DocumentUrl;
            var match = System.Text.RegularExpressions.Regex.Match(
                url,
                @"wikipedia\.org/wiki/([^?#]+)");

            if (match.Success)
            {
                var wikiTitle = match.Groups[1].Value
                    .Replace("_", "-")
                    .ToLowerInvariant();
                return $"nq-wiki-{wikiTitle}";
            }
        }

        // Fallback to entry ID
        return entry.Id;
    }
}

/// <summary>
/// Represents a Natural Questions document for indexing.
/// </summary>
public class NaturalQuestionsDocument
{
    /// <summary>
    /// Unique document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Document title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Original Wikipedia URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Clean text content (converted from HTML).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Metadata about the document.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Internal class for JSON serialization of ground truth entries.
/// </summary>
internal class GroundTruthJsonEntry
{
    public required string Query { get; init; }
    public required string ExpectedAnswer { get; init; }
    public required List<string> RelevantDocuments { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
