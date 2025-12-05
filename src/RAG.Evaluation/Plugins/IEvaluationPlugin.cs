using System.Text.Json;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Plugins;

/// <summary>
/// Interface for dynamically loaded evaluation plugins.
/// Plugins can be implemented to extend the evaluation framework with custom metrics.
/// </summary>
public interface IEvaluationPlugin
{
    /// <summary>
    /// Gets the unique name of this plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of this plugin.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets a description of what this plugin evaluates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluates a single query-response pair and returns a score.
    /// </summary>
    /// <param name="context">The evaluation context containing query, response, and ground truth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation score (typically 0-1, but plugin-specific).</returns>
    Task<double> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the plugin configuration.
    /// </summary>
    /// <param name="config">The configuration JSON element.</param>
    /// <returns>True if the configuration is valid, false otherwise.</returns>
    bool ValidateConfig(JsonElement config);

    /// <summary>
    /// Gets a JSON schema describing the expected configuration format.
    /// </summary>
    /// <returns>JSON schema as a string, or null if no configuration is needed.</returns>
    string? GetConfigSchema();
}
