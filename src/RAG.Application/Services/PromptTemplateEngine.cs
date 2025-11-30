using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Application.Interfaces;
using RAG.Application.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RAG.Application.Services;

/// <summary>
/// Manages YAML-based prompt templates with hot-reload, versioning, and A/B testing.
/// </summary>
public class PromptTemplateEngine : IPromptTemplateEngine, IDisposable
{
    private readonly PromptTemplateSettings _settings;
    private readonly ILogger<PromptTemplateEngine> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ConcurrentDictionary<string, List<PromptTemplate>> _templates;
    private readonly Random _random;
    private FileSystemWatcher? _fileWatcher;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public PromptTemplateEngine(
        IOptions<PromptTemplateSettings> settings,
        ILogger<PromptTemplateEngine> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _settings.Validate();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _templates = new ConcurrentDictionary<string, List<PromptTemplate>>(StringComparer.OrdinalIgnoreCase);
        _random = new Random();

        // Load templates synchronously on startup
        LoadTemplatesSync();

        // Setup hot-reload if enabled
        if (_settings.EnableHotReload)
        {
            SetupFileWatcher();
        }

        _logger.LogInformation(
            "PromptTemplateEngine initialized. Directory: {Directory}, HotReload: {HotReload}, Templates loaded: {Count}",
            _settings.Directory,
            _settings.EnableHotReload,
            _templates.Count);
    }

    /// <inheritdoc/>
    public Task<RenderedPrompt> RenderTemplateAsync(
        string templateName,
        Dictionary<string, string> variables,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be empty", nameof(templateName));

        if (variables == null)
            throw new ArgumentNullException(nameof(variables));

        // Get template
        var template = GetTemplate(templateName, version);
        if (template == null)
        {
            var availableVersions = _templates.ContainsKey(templateName)
                ? string.Join(", ", _templates[templateName].Select(t => t.Version))
                : "none";

            throw new InvalidOperationException(
                $"Template '{templateName}' (version: {version ?? "any"}) not found. Available versions: {availableVersions}");
        }

        // Render with variable substitution
        var systemPrompt = SubstituteVariables(template.SystemPrompt, variables);
        var userPrompt = SubstituteVariables(template.UserPromptTemplate, variables);

        _logger.LogDebug(
            "Rendered template '{TemplateName}' v{Version}. Variables: {VariableCount}",
            template.Name,
            template.Version,
            variables.Count);

        return Task.FromResult(new RenderedPrompt(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            Parameters: template.Parameters,
            TemplateName: template.Name,
            TemplateVersion: template.Version));
    }

    /// <inheritdoc/>
    public PromptTemplate? GetTemplate(string templateName, string? version = null)
    {
        if (!_templates.TryGetValue(templateName, out var versions) || versions.Count == 0)
            return null;

        // If specific version requested
        if (!string.IsNullOrWhiteSpace(version))
        {
            return versions.FirstOrDefault(t => t.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        }

        // If default version configured
        if (!string.IsNullOrWhiteSpace(_settings.DefaultVersion))
        {
            var defaultVersion = versions.FirstOrDefault(t =>
                t.Version.Equals(_settings.DefaultVersion, StringComparison.OrdinalIgnoreCase));
            if (defaultVersion != null)
                return defaultVersion;
        }

        // A/B testing: random selection
        if (_settings.EnableABTesting && versions.Count > 1)
        {
            var selectedIndex = _random.Next(versions.Count);
            var selected = versions[selectedIndex];

            _logger.LogDebug(
                "A/B testing: selected template '{TemplateName}' v{Version} (index {Index} of {Total})",
                templateName,
                selected.Version,
                selectedIndex,
                versions.Count);

            return selected;
        }

        // Default: return latest version (highest version number or last loaded)
        return versions.OrderByDescending(t => t.LoadedAt).First();
    }

    /// <inheritdoc/>
    public IReadOnlyList<PromptTemplate> GetAllTemplates()
    {
        return _templates.Values.SelectMany(v => v).ToList();
    }

    /// <inheritdoc/>
    public async Task ReloadTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Reloading all templates from directory: {Directory}", _settings.Directory);
            _templates.Clear();
            LoadTemplatesSync();
            _logger.LogInformation("Templates reloaded. Total: {Count}", _templates.Count);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<List<string>> ValidateTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        foreach (var kvp in _templates)
        {
            foreach (var template in kvp.Value)
            {
                // Required fields
                if (string.IsNullOrWhiteSpace(template.Name))
                    errors.Add($"Template at '{template.FilePath}': missing 'name' field");

                if (string.IsNullOrWhiteSpace(template.SystemPrompt))
                    errors.Add($"Template '{template.Name}' at '{template.FilePath}': missing 'system_prompt' field");

                if (string.IsNullOrWhiteSpace(template.UserPromptTemplate))
                    errors.Add($"Template '{template.Name}' at '{template.FilePath}': missing 'user_prompt_template' field");

                // Parameters validation
                if (template.Parameters.Temperature < 0 || template.Parameters.Temperature > 1)
                    errors.Add($"Template '{template.Name}' v{template.Version}: temperature must be between 0 and 1");

                if (template.Parameters.MaxTokens <= 0)
                    errors.Add($"Template '{template.Name}' v{template.Version}: max_tokens must be > 0");
            }
        }

        return Task.FromResult(errors);
    }

    private void LoadTemplatesSync()
    {
        var templateDir = Path.IsPathRooted(_settings.Directory)
            ? _settings.Directory
            : Path.Combine(Directory.GetCurrentDirectory(), _settings.Directory);

        if (!Directory.Exists(templateDir))
        {
            _logger.LogWarning("Template directory does not exist: {Directory}. Creating it.", templateDir);
            Directory.CreateDirectory(templateDir);
            return;
        }

        var yamlFiles = Directory.GetFiles(templateDir, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(templateDir, "*.yml", SearchOption.AllDirectories));

        foreach (var filePath in yamlFiles)
        {
            try
            {
                var yamlContent = File.ReadAllText(filePath);
                _logger.LogDebug("Loading template from {FilePath}. Content length: {Length}", filePath, yamlContent.Length);

                var template = _yamlDeserializer.Deserialize<PromptTemplate>(yamlContent);

                if (template == null)
                {
                    _logger.LogWarning("Deserialized template is null for file: {FilePath}", filePath);
                    continue;
                }

                _logger.LogDebug("Deserialized template: Name={Name}, Version={Version}, SystemPrompt={HasSystem}, UserPrompt={HasUser}",
                    template.Name, template.Version, !string.IsNullOrEmpty(template.SystemPrompt), !string.IsNullOrEmpty(template.UserPromptTemplate));

                if (string.IsNullOrWhiteSpace(template.Name))
                {
                    _logger.LogWarning("Template has empty name, skipping file: {FilePath}", filePath);
                    continue;
                }

                template.FilePath = filePath;
                template.LoadedAt = DateTime.UtcNow;

                // Add to collection (keyed by name, multiple versions possible)
                _templates.AddOrUpdate(
                    template.Name,
                    _ => new List<PromptTemplate> { template },
                    (_, existing) =>
                    {
                        existing.Add(template);
                        return existing;
                    });

                _logger.LogDebug(
                    "Loaded template '{TemplateName}' v{Version} from {FilePath}",
                    template.Name,
                    template.Version,
                    filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from {FilePath}. Exception: {ExceptionType}, Message: {Message}",
                    filePath, ex.GetType().Name, ex.Message);
            }
        }
    }

    private void SetupFileWatcher()
    {
        var templateDir = Path.IsPathRooted(_settings.Directory)
            ? _settings.Directory
            : Path.Combine(Directory.GetCurrentDirectory(), _settings.Directory);

        if (!Directory.Exists(templateDir))
        {
            _logger.LogWarning("Cannot setup file watcher: directory does not exist: {Directory}", templateDir);
            return;
        }

        _fileWatcher = new FileSystemWatcher(templateDir)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            Filter = "*.y*ml",
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        _fileWatcher.Changed += OnTemplateFileChanged;
        _fileWatcher.Created += OnTemplateFileChanged;
        _fileWatcher.Deleted += OnTemplateFileChanged;
        _fileWatcher.Renamed += OnTemplateFileRenamed;

        _logger.LogInformation("File watcher enabled for directory: {Directory}", templateDir);
    }

    private void OnTemplateFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Template file {ChangeType}: {FilePath}", e.ChangeType, e.FullPath);

        // Debounce: wait a bit for file to finish writing
        Task.Delay(100).ContinueWith(_ =>
        {
            _ = ReloadTemplatesAsync();
        });
    }

    private void OnTemplateFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation("Template file renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        _ = ReloadTemplatesAsync();
    }

    private string SubstituteVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;

        foreach (var kvp in variables)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}"; // {{key}}
            result = result.Replace(placeholder, kvp.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Warn if unsubstituted placeholders remain
        var remainingPlaceholders = Regex.Matches(result, @"\{\{(\w+)\}\}");
        if (remainingPlaceholders.Count > 0)
        {
            var missing = string.Join(", ", remainingPlaceholders.Select(m => m.Groups[1].Value));
            _logger.LogWarning("Unsubstituted placeholders in template: {Missing}", missing);
        }

        return result;
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _reloadLock?.Dispose();
    }
}
