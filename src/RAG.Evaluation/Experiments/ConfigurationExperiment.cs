using System.Text.Json.Serialization;

namespace RAG.Evaluation.Experiments;

/// <summary>
/// Defines an A/B testing experiment to compare different RAG system configurations.
/// </summary>
public class ConfigurationExperiment
{
    /// <summary>
    /// Name of the experiment.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the experiment is testing.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Name of the dataset to use for evaluation.
    /// </summary>
    [JsonPropertyName("dataset")]
    public required string Dataset { get; init; }

    /// <summary>
    /// Base configuration parameters shared across all variants.
    /// </summary>
    [JsonPropertyName("baseConfiguration")]
    public Dictionary<string, object> BaseConfiguration { get; init; } = new();

    /// <summary>
    /// List of configuration variants to test.
    /// </summary>
    [JsonPropertyName("variants")]
    public List<ExperimentVariant> Variants { get; init; } = new();

    /// <summary>
    /// Metrics to calculate for each variant.
    /// </summary>
    [JsonPropertyName("metrics")]
    public List<string> Metrics { get; init; } = new();

    /// <summary>
    /// The primary metric used for selecting the winner.
    /// </summary>
    [JsonPropertyName("primaryMetric")]
    public string? PrimaryMetric { get; init; }

    /// <summary>
    /// Validates the experiment configuration.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        if (string.IsNullOrWhiteSpace(Dataset))
            return false;

        if (Variants.Count < 2)
            return false;

        if (Metrics.Count == 0)
            return false;

        return Variants.All(v => v.IsValid());
    }
}
