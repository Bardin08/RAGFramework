using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Evaluation.Experiments;

namespace RAG.Evaluation.Services;

/// <summary>
/// Loads experiment configurations from JSON files.
/// </summary>
public class ExperimentConfigurationLoader
{
    private readonly ILogger<ExperimentConfigurationLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentConfigurationLoader"/> class.
    /// </summary>
    public ExperimentConfigurationLoader(ILogger<ExperimentConfigurationLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads an experiment configuration from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded experiment configuration.</returns>
    public async Task<ConfigurationExperiment> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        _logger.LogInformation("Loading experiment configuration from {FilePath}", filePath);

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var experiment = LoadFromJson(json);

            _logger.LogInformation(
                "Loaded experiment: {ExperimentName} with {VariantCount} variants",
                experiment.Name,
                experiment.Variants.Count);

            return experiment;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON configuration file: {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads an experiment configuration from a JSON string.
    /// </summary>
    /// <param name="json">JSON string containing the configuration.</param>
    /// <returns>The loaded experiment configuration.</returns>
    public ConfigurationExperiment LoadFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var experiment = JsonSerializer.Deserialize<ConfigurationExperiment>(json, options);

        if (experiment == null)
        {
            throw new InvalidOperationException("Failed to deserialize experiment configuration");
        }

        if (!experiment.IsValid())
        {
            throw new InvalidOperationException("Invalid experiment configuration");
        }

        return experiment;
    }

    /// <summary>
    /// Saves an experiment configuration to a JSON file.
    /// </summary>
    /// <param name="experiment">The experiment to save.</param>
    /// <param name="filePath">Path where to save the JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveToFileAsync(
        ConfigurationExperiment experiment,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!experiment.IsValid())
        {
            throw new InvalidOperationException("Cannot save invalid experiment configuration");
        }

        _logger.LogInformation("Saving experiment configuration to {FilePath}", filePath);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(experiment, options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Experiment configuration saved successfully");
    }
}
