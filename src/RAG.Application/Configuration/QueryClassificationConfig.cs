namespace RAG.Application.Configuration;

/// <summary>
/// Configuration settings for query classification service.
/// </summary>
public class QueryClassificationConfig
{
    /// <summary>
    /// LLM provider to use for classification ("ollama" or "openai").
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// Model name for classification (e.g., "phi-3:mini", "llama3.1:8b", "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = "phi-3:mini";

    /// <summary>
    /// Timeout for LLM classification requests in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 5000;

    /// <summary>
    /// Whether to enable result caching.
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// How long to cache classification results.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether to fall back to heuristic classification if LLM fails.
    /// </summary>
    public bool FallbackToHeuristics { get; set; } = true;
}
