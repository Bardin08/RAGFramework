namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for BM25 retrieval algorithm.
/// </summary>
public class BM25Settings
{
    /// <summary>
    /// Term frequency saturation parameter (k1).
    /// Controls how quickly term frequency influence saturates.
    /// Default: 1.2 (typical range: 1.2 to 2.0)
    /// </summary>
    public double K1 { get; set; } = 1.2;

    /// <summary>
    /// Length normalization parameter (b).
    /// Controls how much document length affects scoring.
    /// 0 = no length normalization, 1 = full normalization
    /// Default: 0.75 (typical range: 0.5 to 0.9)
    /// </summary>
    public double B { get; set; } = 0.75;

    /// <summary>
    /// Default number of top results to return.
    /// Default: 10
    /// </summary>
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Maximum number of top results allowed.
    /// Default: 100
    /// </summary>
    public int MaxTopK { get; set; } = 100;

    /// <summary>
    /// Timeout for search operations in seconds.
    /// Default: 5 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Fragment size for highlighting in characters.
    /// Default: 150 characters
    /// </summary>
    public int HighlightFragmentSize { get; set; } = 150;

    /// <summary>
    /// Validates the BM25 settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are invalid.</exception>
    public void Validate()
    {
        if (K1 <= 0)
            throw new InvalidOperationException($"BM25Settings.K1 must be positive, got {K1}");

        if (B < 0 || B > 1)
            throw new InvalidOperationException($"BM25Settings.B must be between 0 and 1, got {B}");

        if (DefaultTopK <= 0)
            throw new InvalidOperationException($"BM25Settings.DefaultTopK must be positive, got {DefaultTopK}");

        if (MaxTopK <= 0)
            throw new InvalidOperationException($"BM25Settings.MaxTopK must be positive, got {MaxTopK}");

        if (DefaultTopK > MaxTopK)
            throw new InvalidOperationException($"BM25Settings.DefaultTopK ({DefaultTopK}) cannot exceed MaxTopK ({MaxTopK})");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException($"BM25Settings.TimeoutSeconds must be positive, got {TimeoutSeconds}");

        if (HighlightFragmentSize <= 0)
            throw new InvalidOperationException($"BM25Settings.HighlightFragmentSize must be positive, got {HighlightFragmentSize}");
    }
}
