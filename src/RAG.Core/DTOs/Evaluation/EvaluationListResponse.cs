namespace RAG.Core.DTOs.Evaluation;

/// <summary>
/// Paginated list of evaluations.
/// </summary>
public class EvaluationListResponse
{
    public List<EvaluationResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
