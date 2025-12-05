using System.Text.Json;

namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Response containing evaluation configuration details.
/// </summary>
public class EvaluationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public JsonElement Config { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
