using RAG.Core.Domain;

namespace RAG.Evaluation.Models;

/// <summary>
/// Contains all data needed to evaluate a single query-response pair.
/// </summary>
public class EvaluationContext
{
    /// <summary>
    /// The original user query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The system-generated response to the query.
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// The expected/correct answer for comparison (ground truth).
    /// </summary>
    public string? GroundTruth { get; init; }

    /// <summary>
    /// IDs of documents that should have been retrieved (ground truth relevance).
    /// </summary>
    public IReadOnlyList<Guid> RelevantDocumentIds { get; init; } = [];

    /// <summary>
    /// Documents actually retrieved by the system.
    /// </summary>
    public IReadOnlyList<RetrievalResult> RetrievedDocuments { get; init; } = [];

    /// <summary>
    /// Configuration parameters affecting evaluation behavior.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Unique identifier for this evaluation sample.
    /// </summary>
    public string? SampleId { get; init; }

    /// <summary>
    /// Validates that required fields are present.
    /// </summary>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Query);
}
