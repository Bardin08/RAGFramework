namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Response after starting an evaluation run.
/// </summary>
public class RunEvaluationResponse
{
    public Guid RunId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
