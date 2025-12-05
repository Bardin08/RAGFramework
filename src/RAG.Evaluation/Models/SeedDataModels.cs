namespace RAG.Evaluation.Models;

/// <summary>
/// Represents a document in a seed dataset.
/// </summary>
public record SeedDocument
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Title of the document.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Content/text of the document.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional metadata for the document.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Source identifier (e.g., filename, URL).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Validates that required fields are present.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(Content);
}

/// <summary>
/// Represents a query in a seed dataset with expected results.
/// </summary>
public record SeedQuery
{
    /// <summary>
    /// The query text.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The expected/correct answer for this query.
    /// </summary>
    public string? ExpectedAnswer { get; init; }

    /// <summary>
    /// IDs of documents that should be retrieved for this query.
    /// </summary>
    public required IReadOnlyList<string> RelevantDocIds { get; init; }

    /// <summary>
    /// Optional metadata for the query.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Validates that required fields are present.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Query) &&
        RelevantDocIds.Count > 0;
}

/// <summary>
/// Represents configuration options for the seed dataset.
/// </summary>
public record SeedConfiguration
{
    /// <summary>
    /// Tenant ID to use when loading documents.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Owner ID to use when loading documents.
    /// </summary>
    public string? OwnerId { get; init; }

    /// <summary>
    /// Whether documents should be public.
    /// </summary>
    public bool IsPublic { get; init; } = false;

    /// <summary>
    /// Chunking strategy to use (if different from default).
    /// </summary>
    public string? ChunkingStrategy { get; init; }

    /// <summary>
    /// Additional configuration options.
    /// </summary>
    public Dictionary<string, object>? Options { get; init; }
}

/// <summary>
/// Represents a complete seed dataset file structure.
/// </summary>
public record SeedDatasetFile
{
    /// <summary>
    /// Name of the seed dataset.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Version of the seed dataset.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Description of the seed dataset.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Documents to be loaded.
    /// </summary>
    public required IReadOnlyList<SeedDocument> Documents { get; init; }

    /// <summary>
    /// Queries with expected results for evaluation.
    /// </summary>
    public IReadOnlyList<SeedQuery>? Queries { get; init; }

    /// <summary>
    /// Configuration for loading the dataset.
    /// </summary>
    public SeedConfiguration? Configuration { get; init; }

    /// <summary>
    /// Optional metadata about the dataset.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Validates the seed dataset structure.
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Dataset name is required");

        if (Documents == null || Documents.Count == 0)
            errors.Add("At least one document is required");
        else
        {
            for (int i = 0; i < Documents.Count; i++)
            {
                if (!Documents[i].IsValid())
                    errors.Add($"Document at index {i} is invalid (missing id, title, or content)");
            }

            // Check for duplicate document IDs
            var duplicateIds = Documents
                .GroupBy(d => d.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
                errors.Add($"Duplicate document IDs found: {string.Join(", ", duplicateIds)}");
        }

        if (Queries != null && Queries.Count > 0)
        {
            for (int i = 0; i < Queries.Count; i++)
            {
                if (!Queries[i].IsValid())
                    errors.Add($"Query at index {i} is invalid (missing query or relevantDocIds)");
            }

            // Check that all referenced document IDs exist
            var documentIds = Documents?.Select(d => d.Id).ToHashSet() ?? new HashSet<string>();
            var invalidRefs = Queries
                .SelectMany(q => q.RelevantDocIds)
                .Where(id => !documentIds.Contains(id))
                .Distinct()
                .ToList();

            if (invalidRefs.Any())
                errors.Add($"Queries reference non-existent document IDs: {string.Join(", ", invalidRefs)}");
        }

        return (errors.Count == 0, errors);
    }
}
