using YamlDotNet.Serialization;

namespace RAG.Application.Models;

/// <summary>
/// Represents a YAML-based prompt template with versioning and parameter support.
/// YAML keys use camelCase (name, version, systemPrompt, userPromptTemplate, etc.)
/// </summary>
public class PromptTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public PromptParameters Parameters { get; set; } = new();

    /// <summary>
    /// File path where this template was loaded from.
    /// Not part of YAML, set by loader.
    /// </summary>
    [YamlIgnore]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this template was loaded.
    /// </summary>
    [YamlIgnore]
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Template parameters for LLM generation.
/// </summary>
public class PromptParameters
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 500;
    public Dictionary<string, object> Custom { get; set; } = new();
}
