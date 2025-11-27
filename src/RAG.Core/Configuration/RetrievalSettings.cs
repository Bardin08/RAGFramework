namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for retrieval strategy selection and fallback behavior.
/// </summary>
public class RetrievalSettings
{
    /// <summary>
    /// Default retrieval strategy to use when not explicitly specified.
    /// Valid values: "BM25", "Dense", "Hybrid".
    /// </summary>
    public string DefaultStrategy { get; set; } = "Dense";

    /// <summary>
    /// Enables automatic fallback to an alternative strategy if the primary strategy fails.
    /// </summary>
    public bool EnableStrategyFallback { get; set; } = true;

    /// <summary>
    /// Fallback retrieval strategy to use when primary strategy fails.
    /// Valid values: "BM25", "Dense", "Hybrid".
    /// </summary>
    public string FallbackStrategy { get; set; } = "BM25";

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        var validStrategies = new[] { "BM25", "Dense", "Hybrid" };

        if (string.IsNullOrWhiteSpace(DefaultStrategy))
        {
            throw new ArgumentException("DefaultStrategy cannot be null or empty");
        }

        if (!validStrategies.Contains(DefaultStrategy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Invalid DefaultStrategy: '{DefaultStrategy}'. Must be one of: {string.Join(", ", validStrategies)}");
        }

        if (EnableStrategyFallback)
        {
            if (string.IsNullOrWhiteSpace(FallbackStrategy))
            {
                throw new ArgumentException("FallbackStrategy cannot be null or empty when fallback is enabled");
            }

            if (!validStrategies.Contains(FallbackStrategy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Invalid FallbackStrategy: '{FallbackStrategy}'. Must be one of: {string.Join(", ", validStrategies)}");
            }

            if (DefaultStrategy.Equals(FallbackStrategy, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("FallbackStrategy must be different from DefaultStrategy");
            }
        }
    }
}
