namespace RAG.Application.Configuration;

/// <summary>
/// Configuration settings for the prompt template system.
/// </summary>
public class PromptTemplateSettings
{
    /// <summary>
    /// Directory containing template YAML files.
    /// Relative to application root or absolute path.
    /// </summary>
    public string Directory { get; set; } = "prompts/templates";

    /// <summary>
    /// Enable hot-reload of templates when files change.
    /// </summary>
    public bool EnableHotReload { get; set; } = true;

    /// <summary>
    /// Default template to use when none specified.
    /// </summary>
    public string DefaultTemplate { get; set; } = "rag-answer-generation";

    /// <summary>
    /// Enable A/B testing with random template version selection.
    /// </summary>
    public bool EnableABTesting { get; set; } = false;

    /// <summary>
    /// Specific template version to use (overrides A/B testing).
    /// If null, uses latest version or A/B selection.
    /// </summary>
    public string? DefaultVersion { get; set; }

    /// <summary>
    /// Validate this configuration object.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
            throw new InvalidOperationException("PromptTemplateSettings.Directory must be specified.");

        if (string.IsNullOrWhiteSpace(DefaultTemplate))
            throw new InvalidOperationException("PromptTemplateSettings.DefaultTemplate must be specified.");
    }
}
