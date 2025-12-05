using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Request to run an evaluation.
/// </summary>
public class RunEvaluationRequest
{
    /// <summary>
    /// Optional name for this run (defaults to timestamp-based name).
    /// </summary>
    [StringLength(255)]
    public string? Name { get; set; }

    /// <summary>
    /// Optional description for this run.
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional runtime configuration overrides.
    /// Merged with the evaluation's base config.
    /// </summary>
    public JsonElement? ConfigOverrides { get; set; }
}
