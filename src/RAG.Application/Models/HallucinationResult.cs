namespace RAG.Application.Models;

/// <summary>
/// Result of hallucination detection containing confidence scores and detected issues.
/// </summary>
public record HallucinationResult
{
    /// <summary>
    /// Overall confidence score (0.0 to 1.0) that the response is grounded and factual.
    /// </summary>
    public decimal OverallConfidence { get; init; }

    /// <summary>
    /// Confidence level classification based on overall score.
    /// </summary>
    public ConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>
    /// Score from context grounding check (0.0 to 1.0).
    /// </summary>
    public decimal GroundingScore { get; init; }

    /// <summary>
    /// Score from self-consistency check (0.0 to 1.0), null if not performed.
    /// </summary>
    public decimal? ConsistencyScore { get; init; }

    /// <summary>
    /// Score from LLM-as-a-judge fact-checking (0.0 to 1.0), null if not performed.
    /// </summary>
    public decimal? FaithfulnessScore { get; init; }

    /// <summary>
    /// List of detected issues or potential hallucinations.
    /// </summary>
    public List<string> Issues { get; init; } = new();

    /// <summary>
    /// Indicates whether the response should be flagged for human review.
    /// </summary>
    public bool RequiresHumanReview { get; init; }

    /// <summary>
    /// Detailed breakdown of grounding scores per claim/sentence.
    /// </summary>
    public List<ClaimGrounding> ClaimGroundings { get; init; } = new();
}

/// <summary>
/// Confidence level classification.
/// </summary>
public enum ConfidenceLevel
{
    Low,    // < 0.7
    Medium, // 0.7 - 0.85
    High    // > 0.85
}

/// <summary>
/// Grounding score for individual claim or sentence.
/// </summary>
public record ClaimGrounding
{
    /// <summary>
    /// The claim or sentence being evaluated.
    /// </summary>
    public string Claim { get; init; } = string.Empty;

    /// <summary>
    /// Grounding score for this claim (0.0 to 1.0).
    /// </summary>
    public decimal Score { get; init; }

    /// <summary>
    /// Whether this claim is considered grounded (score above threshold).
    /// </summary>
    public bool IsGrounded { get; init; }
}
