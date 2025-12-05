using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Request to create a new evaluation configuration.
/// </summary>
public class CreateEvaluationRequest
{
    /// <summary>
    /// Unique name for this evaluation.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this evaluation measures.
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of evaluation (e.g., "retrieval", "generation", "end-to-end").
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Configuration as JSON object.
    /// Should contain metrics, thresholds, dataset references, etc.
    /// </summary>
    [Required]
    public JsonElement Config { get; set; }
}
