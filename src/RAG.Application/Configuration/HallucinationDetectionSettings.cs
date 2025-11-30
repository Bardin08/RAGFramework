namespace RAG.Application.Configuration;

/// <summary>
/// Configuration settings for hallucination detection.
/// </summary>
public class HallucinationDetectionSettings
{
    /// <summary>
    /// Enable context grounding check (n-gram and semantic similarity).
    /// </summary>
    public bool EnableContextGrounding { get; set; } = true;

    /// <summary>
    /// Enable self-consistency check (multiple generations compared).
    /// </summary>
    public bool EnableSelfConsistency { get; set; } = false;

    /// <summary>
    /// Enable LLM-as-a-judge fact-checking.
    /// </summary>
    public bool EnableLLMJudge { get; set; } = false;

    /// <summary>
    /// Minimum grounding score threshold (0.0 to 1.0).
    /// Claims below this threshold are flagged.
    /// </summary>
    public decimal GroundingThreshold { get; set; } = 0.7m;

    /// <summary>
    /// Minimum consistency score threshold (0.0 to 1.0).
    /// Scores below this indicate potential hallucination.
    /// </summary>
    public decimal ConsistencyThreshold { get; set; } = 0.6m;

    /// <summary>
    /// Minimum overall confidence score (0.0 to 1.0).
    /// Responses below this may require human review.
    /// </summary>
    public decimal MinConfidence { get; set; } = 0.7m;

    /// <summary>
    /// Enable human review queue for low-confidence responses.
    /// </summary>
    public bool EnableHumanReview { get; set; } = false;

    /// <summary>
    /// Weight for grounding score in overall confidence calculation.
    /// </summary>
    public decimal GroundingWeight { get; set; } = 0.5m;

    /// <summary>
    /// Weight for consistency score in overall confidence calculation.
    /// </summary>
    public decimal ConsistencyWeight { get; set; } = 0.3m;

    /// <summary>
    /// Weight for faithfulness score in overall confidence calculation.
    /// </summary>
    public decimal FaithfulnessWeight { get; set; } = 0.2m;
}
