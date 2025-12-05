using System.Text.Json.Serialization;

namespace RAG.Evaluation.Experiments;

/// <summary>
/// Represents a single configuration variant in an A/B test experiment.
/// </summary>
public class ExperimentVariant
{
    /// <summary>
    /// Name of this variant.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Configuration parameters specific to this variant.
    /// </summary>
    [JsonPropertyName("parameters")]
    public VariantParameters Parameters { get; init; } = new();

    /// <summary>
    /// Validates the variant configuration.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// Merges this variant's parameters with base configuration.
    /// </summary>
    public Dictionary<string, object> MergeWithBase(Dictionary<string, object> baseConfig)
    {
        var merged = new Dictionary<string, object>(baseConfig);

        if (Parameters.RetrievalStrategy != null)
            merged["retrievalStrategy"] = Parameters.RetrievalStrategy;

        if (Parameters.HybridAlpha.HasValue)
            merged["hybridAlpha"] = Parameters.HybridAlpha.Value;

        if (Parameters.HybridBeta.HasValue)
            merged["hybridBeta"] = Parameters.HybridBeta.Value;

        if (Parameters.RRFk.HasValue)
            merged["rrfK"] = Parameters.RRFk.Value;

        if (Parameters.TopK.HasValue)
            merged["topK"] = Parameters.TopK.Value;

        if (Parameters.LLMTemperature.HasValue)
            merged["llmTemperature"] = Parameters.LLMTemperature.Value;

        return merged;
    }
}

/// <summary>
/// Configuration parameters that can vary between experiment variants.
/// </summary>
public class VariantParameters
{
    /// <summary>
    /// Retrieval strategy (e.g., "bm25", "dense", "hybrid", "rrf").
    /// </summary>
    [JsonPropertyName("retrievalStrategy")]
    public string? RetrievalStrategy { get; init; }

    /// <summary>
    /// Alpha parameter for hybrid search weighting.
    /// </summary>
    [JsonPropertyName("hybridAlpha")]
    public double? HybridAlpha { get; init; }

    /// <summary>
    /// Beta parameter for hybrid search weighting.
    /// </summary>
    [JsonPropertyName("hybridBeta")]
    public double? HybridBeta { get; init; }

    /// <summary>
    /// K parameter for Reciprocal Rank Fusion.
    /// </summary>
    [JsonPropertyName("rrfK")]
    public int? RRFk { get; init; }

    /// <summary>
    /// Number of top documents to retrieve.
    /// </summary>
    [JsonPropertyName("topK")]
    public int? TopK { get; init; }

    /// <summary>
    /// Temperature parameter for LLM generation.
    /// </summary>
    [JsonPropertyName("llmTemperature")]
    public double? LLMTemperature { get; init; }
}
