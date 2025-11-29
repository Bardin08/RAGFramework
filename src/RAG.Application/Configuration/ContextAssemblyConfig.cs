namespace RAG.Application.Configuration;

/// <summary>
/// Configuration for context assembly logic.
/// </summary>
public class ContextAssemblyConfig
{
    /// <summary>
    /// Maximum token limit for assembled context.
    /// Default: 3000 tokens (conservative for most LLMs).
    /// </summary>
    public int MaxTokens { get; set; } = 3000;

    /// <summary>
    /// Minimum relevance score threshold for including results.
    /// Results below this score are filtered out.
    /// Default: 0.3 (30%).
    /// </summary>
    public double MinScore { get; set; } = 0.3;

    /// <summary>
    /// Token counter strategy to use.
    /// Options: "Approximate" (default) or "TikToken".
    /// </summary>
    public string TokenCounterStrategy { get; set; } = "Approximate";

    /// <summary>
    /// Enable deduplication of similar chunks.
    /// Default: true.
    /// </summary>
    public bool EnableDeduplication { get; set; } = true;
}
