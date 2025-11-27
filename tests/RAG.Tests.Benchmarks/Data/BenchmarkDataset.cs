using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Tests.Benchmarks.Data;

/// <summary>
/// Represents a benchmark document for retrieval testing.
/// </summary>
public record BenchmarkDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }
}

/// <summary>
/// Represents a benchmark query with ground truth relevance judgments.
/// </summary>
public record BenchmarkQuery
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("relevantDocIds")]
    public required List<string> RelevantDocIds { get; init; }

    [JsonPropertyName("queryType")]
    public required string QueryType { get; init; }
}

/// <summary>
/// Represents the complete benchmark dataset with documents and queries.
/// </summary>
public record BenchmarkDataset
{
    [JsonPropertyName("documents")]
    public required List<BenchmarkDocument> Documents { get; init; }

    [JsonPropertyName("queries")]
    public required List<BenchmarkQuery> Queries { get; init; }

    /// <summary>
    /// Loads benchmark dataset from JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <returns>Loaded benchmark dataset.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the file contains invalid JSON.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the dataset validation fails.</exception>
    public static BenchmarkDataset LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Benchmark dataset file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        var dataset = JsonSerializer.Deserialize<BenchmarkDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dataset == null)
        {
            throw new JsonException("Failed to deserialize benchmark dataset.");
        }

        // Validate dataset
        if (dataset.Documents.Count == 0)
        {
            throw new InvalidOperationException("Benchmark dataset must contain at least one document.");
        }

        if (dataset.Queries.Count == 0)
        {
            throw new InvalidOperationException("Benchmark dataset must contain at least one query.");
        }

        // Validate relevance judgments
        var documentIds = dataset.Documents.Select(d => d.Id).ToHashSet();
        foreach (var query in dataset.Queries)
        {
            if (query.RelevantDocIds.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Query '{query.Id}' must have at least one relevant document.");
            }

            foreach (var docId in query.RelevantDocIds)
            {
                if (!documentIds.Contains(docId))
                {
                    throw new InvalidOperationException(
                        $"Query '{query.Id}' references non-existent document '{docId}'.");
                }
            }
        }

        return dataset;
    }

    /// <summary>
    /// Gets all unique query types in the dataset.
    /// </summary>
    public HashSet<string> GetQueryTypes()
    {
        return Queries.Select(q => q.QueryType).ToHashSet();
    }

    /// <summary>
    /// Gets queries filtered by query type.
    /// </summary>
    public List<BenchmarkQuery> GetQueriesByType(string queryType)
    {
        return Queries.Where(q => q.QueryType == queryType).ToList();
    }
}
