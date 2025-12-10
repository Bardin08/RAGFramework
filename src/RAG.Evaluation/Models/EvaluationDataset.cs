namespace RAG.Evaluation.Models;

/// <summary>
/// A collection of evaluation samples to run metrics against.
/// </summary>
public class EvaluationDataset
{
    /// <summary>
    /// Name/identifier of this dataset.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of the dataset purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The evaluation samples in this dataset.
    /// </summary>
    public IReadOnlyList<EvaluationContext> Samples { get; init; } = [];

    /// <summary>
    /// Dataset-level metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Version of this dataset.
    /// </summary>
    public string Version { get; init; } = "1.0";
}
