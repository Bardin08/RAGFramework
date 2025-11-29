namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for Hybrid retrieval strategy.
/// Combines BM25 and Dense retrieval with configurable weights.
/// </summary>
public class HybridSearchConfig
{
    /// <summary>
    /// Weight for BM25 retrieval scores (keyword-based).
    /// Default: 0.5. Must be in range [0, 1] and Alpha + Beta must equal 1.0.
    /// </summary>
    public double Alpha { get; set; } = 0.5;

    /// <summary>
    /// Weight for Dense retrieval scores (semantic).
    /// Default: 0.5. Must be in range [0, 1] and Alpha + Beta must equal 1.0.
    /// </summary>
    public double Beta { get; set; } = 0.5;

    /// <summary>
    /// Number of intermediate results to retrieve from each retrieval strategy (BM25, Dense)
    /// before combining and re-ranking.
    /// Default: 20. Higher values improve recall but increase latency.
    /// </summary>
    public int IntermediateK { get; set; } = 20;

    /// <summary>
    /// Reranking method for combining BM25 and Dense results.
    /// Valid values: "Weighted" (alpha/beta weighted scoring) or "RRF" (Reciprocal Rank Fusion).
    /// Default: "Weighted" (preserves existing behavior).
    /// Note: Alpha and Beta are only used when RerankingMethod is "Weighted".
    /// </summary>
    public string RerankingMethod { get; set; } = "Weighted";

    /// <summary>
    /// Validates the configuration settings.
    /// Ensures Alpha + Beta = 1.0 and all values are within valid ranges.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    public void Validate()
    {
        if (Alpha < 0 || Alpha > 1)
        {
            throw new InvalidOperationException($"Alpha must be in range [0, 1]. Current value: {Alpha}");
        }

        if (Beta < 0 || Beta > 1)
        {
            throw new InvalidOperationException($"Beta must be in range [0, 1]. Current value: {Beta}");
        }

        const double tolerance = 0.0001; // Floating point comparison tolerance
        if (Math.Abs(Alpha + Beta - 1.0) > tolerance)
        {
            throw new InvalidOperationException(
                $"Alpha + Beta must equal 1.0. Current values: Alpha={Alpha}, Beta={Beta}, Sum={Alpha + Beta}");
        }

        if (IntermediateK <= 0)
        {
            throw new InvalidOperationException($"IntermediateK must be greater than 0. Current value: {IntermediateK}");
        }

        if (IntermediateK > 100)
        {
            throw new InvalidOperationException($"IntermediateK cannot exceed 100 (performance limit). Current value: {IntermediateK}");
        }

        // Validate RerankingMethod
        if (!string.Equals(RerankingMethod, "Weighted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(RerankingMethod, "RRF", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"RerankingMethod must be 'Weighted' or 'RRF'. Current value: '{RerankingMethod}'");
        }
    }
}
