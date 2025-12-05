using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.GroundTruth;

/// <summary>
/// Loads ground truth data from JSON format.
/// </summary>
public class JsonGroundTruthLoader : IGroundTruthLoader
{
    private readonly ILogger<JsonGroundTruthLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonGroundTruthLoader(ILogger<JsonGroundTruthLoader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedExtensions => [".json"];

    public bool CanHandle(string path) =>
        Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<GroundTruthDataset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading JSON ground truth from: {Path}", path);

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
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var rawEntries = JsonSerializer.Deserialize<List<JsonGroundTruthEntry>>(json, JsonOptions);

            if (rawEntries is null || rawEntries.Count == 0)
            {
                return new GroundTruthDataset
                {
                    SourcePath = path,
                    ValidationErrors = ["No entries found in JSON file"]
                };
            }

            var entries = new List<GroundTruthEntry>();
            var errors = new List<string>();

            for (var i = 0; i < rawEntries.Count; i++)
            {
                var raw = rawEntries[i];
                var entryErrors = ValidateEntry(raw, i);

                if (entryErrors.Count > 0)
                {
                    errors.AddRange(entryErrors);
                    continue;
                }

                entries.Add(new GroundTruthEntry(
                    raw.Query!,
                    raw.ExpectedAnswer ?? string.Empty,
                    raw.RelevantDocuments ?? []));
            }

            _logger.LogInformation(
                "Loaded {EntryCount} ground truth entries from JSON, {ErrorCount} validation errors",
                entries.Count, errors.Count);

            return new GroundTruthDataset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                SourcePath = path,
                Entries = entries,
                ValidationErrors = errors
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON ground truth file: {Path}", path);
            return new GroundTruthDataset
            {
                SourcePath = path,
                ValidationErrors = [$"JSON parse error: {ex.Message}"]
            };
        }
    }

    private static List<string> ValidateEntry(JsonGroundTruthEntry entry, int index)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entry.Query))
            errors.Add($"Entry {index}: Query is required");

        if (entry.RelevantDocuments is null || entry.RelevantDocuments.Count == 0)
            errors.Add($"Entry {index}: At least one relevant document is required");

        return errors;
    }

    private record JsonGroundTruthEntry(
        string? Query,
        string? ExpectedAnswer,
        List<string>? RelevantDocuments);
}
