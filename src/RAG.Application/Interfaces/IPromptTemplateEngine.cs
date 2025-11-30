using RAG.Application.Models;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for loading, managing, and rendering YAML-based prompt templates.
/// Supports hot-reload, versioning, and A/B testing.
/// </summary>
public interface IPromptTemplateEngine
{
    /// <summary>
    /// Render a prompt template with variable substitution.
    /// </summary>
    /// <param name="templateName">Name of the template to render</param>
    /// <param name="variables">Variables for substitution (e.g., {{context}}, {{query}})</param>
    /// <param name="version">Optional version to use (defaults to latest or configured version)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered prompt with system and user components</returns>
    Task<RenderedPrompt> RenderTemplateAsync(
        string templateName,
        Dictionary<string, string> variables,
        string? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template by name and optional version.
    /// </summary>
    /// <param name="templateName">Template name</param>
    /// <param name="version">Optional version (null = latest/default)</param>
    /// <returns>Template if found, null otherwise</returns>
    PromptTemplate? GetTemplate(string templateName, string? version = null);

    /// <summary>
    /// Get all available templates.
    /// </summary>
    /// <returns>List of all loaded templates</returns>
    IReadOnlyList<PromptTemplate> GetAllTemplates();

    /// <summary>
    /// Reload all templates from disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReloadTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate all templates for required fields and YAML syntax.
    /// </summary>
    /// <returns>List of validation errors (empty if all valid)</returns>
    Task<List<string>> ValidateTemplatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Rendered prompt with system and user components.
/// </summary>
public record RenderedPrompt(
    string SystemPrompt,
    string UserPrompt,
    PromptParameters Parameters,
    string TemplateName,
    string TemplateVersion);
