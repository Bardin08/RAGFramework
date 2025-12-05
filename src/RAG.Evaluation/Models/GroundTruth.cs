namespace RAG.Evaluation.Models;

/// <summary>
/// Represents a single ground truth entry for evaluation.
/// </summary>
/// <param name="Query">The query to evaluate.</param>
/// <param name="ExpectedAnswer">The expected/correct answer.</param>
/// <param name="RelevantDocumentIds">IDs of documents that should be retrieved for this query.</param>
public record GroundTruthEntry(
    string Query,
    string ExpectedAnswer,
    IReadOnlyList<string> RelevantDocumentIds)
{
    /// <summary>
    /// Alternative valid answers/aliases (e.g., for TriviaQA).
    /// If null or empty, only ExpectedAnswer is considered valid.
    /// </summary>
    public IReadOnlyList<string>? AnswerAliases { get; init; }

    /// <summary>
    /// Additional metadata for this entry (e.g., question ID, source).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets all valid answers including the primary answer and aliases.
    /// </summary>
    public IEnumerable<string> GetAllValidAnswers()
    {
        yield return ExpectedAnswer;

        if (AnswerAliases != null)
        {
            foreach (var alias in AnswerAliases)
            {
                if (!string.IsNullOrWhiteSpace(alias) && !alias.Equals(ExpectedAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    yield return alias;
                }
            }
        }
    }

    /// <summary>
    /// Validates that required fields are present.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Query) &&
        RelevantDocumentIds.Count > 0;
}

/// <summary>
/// A collection of ground truth entries for evaluation.
/// </summary>
public class GroundTruthDataset
{
    /// <summary>
    /// Name/identifier of this dataset.
    /// </summary>
    public string Name { get; init; } = "Default";

    /// <summary>
    /// Optional description of the dataset.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The ground truth entries.
    /// </summary>
    public IReadOnlyList<GroundTruthEntry> Entries { get; init; } = [];

    /// <summary>
    /// Source file path if loaded from file.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// When the dataset was loaded.
    /// </summary>
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Validation errors encountered during loading.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Returns true if the dataset is valid and has no errors.
    /// </summary>
    public bool IsValid => ValidationErrors.Count == 0 && Entries.Count > 0;
}
