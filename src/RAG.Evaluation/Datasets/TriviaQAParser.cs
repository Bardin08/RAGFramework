using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Parser for TriviaQA dataset format.
/// Handles parsing of TriviaQA JSON files and extraction of evidence documents.
/// </summary>
public class TriviaQAParser
{
    private readonly ILogger<TriviaQAParser> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public TriviaQAParser(ILogger<TriviaQAParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a TriviaQA JSON file and extracts entries.
    /// </summary>
    /// <param name="filePath">Path to the TriviaQA JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parsed TriviaQA entries.</returns>
    public async Task<List<TriviaQAEntry>> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing TriviaQA file: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"TriviaQA file not found: {filePath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            // First, try TriviaQA object format with "Data" array
            try
            {
                var rootObject = JsonSerializer.Deserialize<TriviaQARootObject>(json, JsonOptions);

                if (rootObject?.Data != null && rootObject.Data.Count > 0)
                {
                    _logger.LogInformation("Parsed {Count} TriviaQA entries from object format", rootObject.Data.Count);
                    return rootObject.Data;
                }
            }
            catch (JsonException)
            {
                // Not an object format, try array format below
            }

            // Try parsing as direct array
            try
            {
                var entries = JsonSerializer.Deserialize<List<TriviaQAEntry>>(json, JsonOptions);

                if (entries != null && entries.Count > 0)
                {
                    _logger.LogInformation("Parsed {Count} TriviaQA entries from array format", entries.Count);
                    return entries;
                }
            }
            catch (JsonException)
            {
                // Not an array format either
            }

            _logger.LogWarning("No entries found in TriviaQA file: {FilePath}", filePath);
            return new List<TriviaQAEntry>();
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Failed to parse TriviaQA JSON file: {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid TriviaQA JSON format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts evidence documents from TriviaQA entries.
    /// </summary>
    /// <param name="entries">TriviaQA entries to extract documents from.</param>
    /// <returns>List of extracted documents.</returns>
    public List<TriviaQADocument> ExtractDocuments(List<TriviaQAEntry> entries)
    {
        _logger.LogInformation("Extracting documents from {Count} TriviaQA entries", entries.Count);

        var documents = new List<TriviaQADocument>();
        var documentIdSet = new HashSet<string>();

        foreach (var entry in entries)
        {
            // Extract Wikipedia evidence documents
            if (entry.EntityPages != null)
            {
                foreach (var page in entry.EntityPages)
                {
                    var doc = ExtractWikipediaDocument(entry.QuestionId, page);
                    if (doc != null && documentIdSet.Add(doc.DocumentId))
                    {
                        documents.Add(doc);
                    }
                }
            }

            // Extract web search evidence documents
            if (entry.SearchResults != null)
            {
                foreach (var result in entry.SearchResults)
                {
                    var doc = ExtractWebDocument(entry.QuestionId, result);
                    if (doc != null && documentIdSet.Add(doc.DocumentId))
                    {
                        documents.Add(doc);
                    }
                }
            }
        }

        _logger.LogInformation("Extracted {Count} unique documents from TriviaQA entries", documents.Count);
        return documents;
    }

    /// <summary>
    /// Converts TriviaQA entries to ground truth dataset.
    /// </summary>
    /// <param name="entries">TriviaQA entries to convert.</param>
    /// <param name="datasetName">Name for the dataset.</param>
    /// <returns>Ground truth dataset with all answer aliases.</returns>
    public GroundTruthDataset ConvertToGroundTruth(List<TriviaQAEntry> entries, string datasetName = "TriviaQA")
    {
        _logger.LogInformation("Converting {Count} TriviaQA entries to ground truth", entries.Count);

        var groundTruthEntries = new List<GroundTruthEntry>();
        var validationErrors = new List<string>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Question))
            {
                validationErrors.Add($"Question ID {entry.QuestionId}: Empty question");
                continue;
            }

            if (entry.Answer == null || string.IsNullOrWhiteSpace(entry.Answer.Value))
            {
                validationErrors.Add($"Question ID {entry.QuestionId}: Missing answer");
                continue;
            }

            // Extract relevant document IDs
            var relevantDocIds = new List<string>();

            if (entry.EntityPages != null)
            {
                relevantDocIds.AddRange(
                    entry.EntityPages
                        .Where(p => !string.IsNullOrWhiteSpace(p.DocSource))
                        .Select(p => GenerateDocumentId(entry.QuestionId, p.DocSource!, "wiki"))
                );
            }

            if (entry.SearchResults != null)
            {
                relevantDocIds.AddRange(
                    entry.SearchResults
                        .Where(r => !string.IsNullOrWhiteSpace(r.DocSource))
                        .Select(r => GenerateDocumentId(entry.QuestionId, r.DocSource!, "web"))
                );
            }

            if (relevantDocIds.Count == 0)
            {
                validationErrors.Add($"Question ID {entry.QuestionId}: No relevant documents");
                continue;
            }

            // For TriviaQA, we store the primary answer and all aliases
            var expectedAnswer = entry.Answer.Value;
            var answerAliases = entry.Answer.GetAllValidAnswers()
                .Where(a => !a.Equals(expectedAnswer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Add metadata for evaluation
            var metadata = new Dictionary<string, object>
            {
                ["questionId"] = entry.QuestionId,
                ["normalizedAliases"] = entry.Answer.GetAllNormalizedAnswers().Distinct().ToList()
            };

            if (!string.IsNullOrWhiteSpace(entry.QuestionSource))
            {
                metadata["questionSource"] = entry.QuestionSource;
            }

            groundTruthEntries.Add(new GroundTruthEntry(
                Query: entry.Question,
                ExpectedAnswer: expectedAnswer,
                RelevantDocumentIds: relevantDocIds
            )
            {
                AnswerAliases = answerAliases,
                Metadata = metadata
            });
        }

        _logger.LogInformation(
            "Created {Count} ground truth entries with {ErrorCount} validation errors",
            groundTruthEntries.Count, validationErrors.Count);

        return new GroundTruthDataset
        {
            Name = datasetName,
            Description = "TriviaQA benchmark dataset for question answering evaluation",
            Entries = groundTruthEntries,
            ValidationErrors = validationErrors
        };
    }

    /// <summary>
    /// Saves documents to the processed directory.
    /// </summary>
    /// <param name="documents">Documents to save.</param>
    /// <param name="outputDirectory">Output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveDocumentsAsync(
        List<TriviaQADocument> documents,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving {Count} documents to {Directory}", documents.Count, outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var tasks = documents.Select(async doc =>
        {
            var filePath = Path.Combine(outputDirectory, $"{doc.DocumentId}.json");
            var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Saved {Count} documents to {Directory}", documents.Count, outputDirectory);
    }

    /// <summary>
    /// Saves ground truth to JSON file.
    /// </summary>
    /// <param name="groundTruth">Ground truth dataset to save.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveGroundTruthAsync(
        GroundTruthDataset groundTruth,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving ground truth to {Path}", outputPath);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Convert to the format expected by JsonGroundTruthLoader
        var entries = groundTruth.Entries.Select(e => new
        {
            Query = e.Query,
            ExpectedAnswer = e.ExpectedAnswer,
            RelevantDocuments = e.RelevantDocumentIds.ToList(),
            AnswerAliases = e.AnswerAliases?.ToList(),
            Metadata = e.Metadata
        }).ToList();

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        _logger.LogInformation("Saved {Count} ground truth entries to {Path}", entries.Count, outputPath);
    }

    private TriviaQADocument? ExtractWikipediaDocument(string questionId, TriviaQAEntityPage page)
    {
        if (string.IsNullOrWhiteSpace(page.WikipediaText) || string.IsNullOrWhiteSpace(page.DocSource))
        {
            return null;
        }

        return new TriviaQADocument
        {
            DocumentId = GenerateDocumentId(questionId, page.DocSource, "wiki"),
            QuestionId = questionId,
            Title = page.Title ?? "Untitled Wikipedia Page",
            Content = page.WikipediaText,
            Source = "Wikipedia",
            Metadata = new Dictionary<string, object>
            {
                ["filename"] = page.Filename ?? string.Empty,
                ["docSource"] = page.DocSource
            }
        };
    }

    private TriviaQADocument? ExtractWebDocument(string questionId, TriviaQASearchResult result)
    {
        // Prefer PageText, fall back to Description
        var content = result.PageText ?? result.Description;

        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(result.DocSource))
        {
            return null;
        }

        var metadata = new Dictionary<string, object>
        {
            ["filename"] = result.Filename ?? string.Empty,
            ["docSource"] = result.DocSource
        };

        if (result.Rank.HasValue)
        {
            metadata["searchRank"] = result.Rank.Value;
        }

        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            metadata["description"] = result.Description;
        }

        return new TriviaQADocument
        {
            DocumentId = GenerateDocumentId(questionId, result.DocSource, "web"),
            QuestionId = questionId,
            Title = result.Title ?? "Untitled Web Page",
            Content = content,
            Source = "Web",
            Url = result.Url,
            Metadata = metadata
        };
    }

    private static string GenerateDocumentId(string questionId, string docSource, string sourceType)
    {
        // Create a unique, deterministic document ID
        var sanitized = docSource.Replace("/", "_").Replace("\\", "_");
        return $"triviaqa-{sourceType}-{questionId}-{sanitized}";
    }

    /// <summary>
    /// Root object for TriviaQA JSON format.
    /// </summary>
    private class TriviaQARootObject
    {
        [JsonPropertyName("Data")]
        public List<TriviaQAEntry>? Data { get; set; }

        [JsonPropertyName("Version")]
        public string? Version { get; set; }
    }
}
