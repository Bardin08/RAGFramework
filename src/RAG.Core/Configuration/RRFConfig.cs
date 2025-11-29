namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for Reciprocal Rank Fusion (RRF) reranking algorithm.
/// Based on research by Cormack, Clarke, Buettcher (SIGIR 2009).
/// </summary>
public class RRFConfig
{
    /// <summary>
    /// RRF constant parameter (k) used in the formula: RRF(d) = Σ 1 / (k + rank(d)).
    /// Default: 60 (empirically validated optimal value from research literature).
    /// Higher values reduce the impact of rank differences, lower values emphasize top-ranked documents.
    /// </summary>
    public int K { get; set; } = 60;

    /// <summary>
    /// Validates the RRF configuration settings.
    /// Ensures K is within valid range for meaningful RRF scoring.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (K <= 0)
        {
            throw new InvalidOperationException($"RRFConfig.K must be greater than 0, got {K}");
        }

        if (K > 200)
        {
            throw new InvalidOperationException(
                $"RRFConfig.K exceeds recommended maximum (200), got {K}. " +
                "High K values may reduce ranking effectiveness.");
        }
    }
}
