namespace RAG.Core.Domain;

/// <summary>
/// Represents an evaluation configuration that can be run multiple times.
/// </summary>
public class Evaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable name for this evaluation configuration.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this evaluation measures.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of evaluation (e.g., "retrieval", "generation", "end-to-end").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Configuration in JSON format.
    /// Contains metrics to run, thresholds, dataset references, etc.
    /// </summary>
    public string Config { get; set; } = "{}";

    /// <summary>
    /// When this evaluation configuration was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// User who created this evaluation.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Whether this evaluation is active/enabled.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time this evaluation was modified.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// User who last updated this evaluation.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}
