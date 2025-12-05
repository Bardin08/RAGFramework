using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Request to update an existing evaluation configuration.
/// </summary>
public class UpdateEvaluationRequest
{
    /// <summary>
    /// Updated name for the evaluation.
    /// </summary>
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>
    /// Updated description.
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Updated type.
    /// </summary>
    [StringLength(50)]
    public string? Type { get; set; }

    /// <summary>
    /// Updated configuration.
    /// </summary>
    public JsonElement? Config { get; set; }

    /// <summary>
    /// Whether the evaluation is active.
    /// </summary>
    public bool? IsActive { get; set; }
}
