using Microsoft.Extensions.Logging;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.GroundTruth;

/// <summary>
/// Loads ground truth data from CSV format.
/// Expected format: query,expected_answer,relevant_docs (semicolon-separated document IDs)
/// </summary>
public class CsvGroundTruthLoader : IGroundTruthLoader
{
    private readonly ILogger<CsvGroundTruthLoader> _logger;

    public CsvGroundTruthLoader(ILogger<CsvGroundTruthLoader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedExtensions => [".csv"];

    public bool CanHandle(string path) =>
        Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<GroundTruthDataset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading CSV ground truth from: {Path}", path);

        if (!File.Exists(path))
        {
            return new GroundTruthDataset
            {
                SourcePath = path,
                ValidationErrors = [$"File not found: {path}"]
            };
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);

            if (lines.Length < 2)
            {
                return new GroundTruthDataset
                {
                    SourcePath = path,
                    ValidationErrors = ["CSV file must have header row and at least one data row"]
                };
            }

            var entries = new List<GroundTruthEntry>();
            var errors = new List<string>();

            // Skip header row
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parseResult = ParseCsvLine(line, i);
                if (parseResult.Error is not null)
                {
                    errors.Add(parseResult.Error);
                    continue;
                }

                if (parseResult.Entry is not null)
                {
                    var entryErrors = ValidateEntry(parseResult.Entry, i);
                    if (entryErrors.Count > 0)
                    {
                        errors.AddRange(entryErrors);
                        continue;
                    }

                    entries.Add(parseResult.Entry);
                }
            }

            _logger.LogInformation(
                "Loaded {EntryCount} ground truth entries from CSV, {ErrorCount} validation errors",
                entries.Count, errors.Count);

            return new GroundTruthDataset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                SourcePath = path,
                Entries = entries,
                ValidationErrors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV ground truth file: {Path}", path);
            return new GroundTruthDataset
            {
                SourcePath = path,
                ValidationErrors = [$"CSV parse error: {ex.Message}"]
            };
        }
    }

    private static (GroundTruthEntry? Entry, string? Error) ParseCsvLine(string line, int lineNumber)
    {
        var fields = ParseCsvFields(line);

        if (fields.Count < 3)
        {
            return (null, $"Line {lineNumber}: Expected 3 fields (query, expected_answer, relevant_docs), got {fields.Count}");
        }

        var query = fields[0];
        var expectedAnswer = fields[1];
        var relevantDocs = fields[2]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return (new GroundTruthEntry(query, expectedAnswer, relevantDocs), null);
    }

    /// <summary>
    /// Parses a CSV line handling quoted values with commas.
    /// </summary>
    private static List<string> ParseCsvFields(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString().Trim());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString().Trim());
        return fields;
    }

    private static List<string> ValidateEntry(GroundTruthEntry entry, int lineNumber)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entry.Query))
            errors.Add($"Line {lineNumber}: Query is required");

        if (entry.RelevantDocumentIds.Count == 0)
            errors.Add($"Line {lineNumber}: At least one relevant document is required");

        return errors;
    }
}
