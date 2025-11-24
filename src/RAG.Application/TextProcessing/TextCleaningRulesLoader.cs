using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Configuration;

namespace RAG.Application.TextProcessing;

/// <summary>
/// Loads text cleaning rules from configuration files.
/// </summary>
public class TextCleaningRulesLoader
{
    private readonly TextCleaningSettings _settings;
    private readonly ILogger<TextCleaningRulesLoader> _logger;
    private readonly string _baseDirectory;
    private TextCleaningRuleSet? _cachedRules;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public TextCleaningRulesLoader(
        IOptions<TextCleaningSettings> settings,
        ILogger<TextCleaningRulesLoader> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _settings.Validate();

        // Resolve base directory (support both relative and absolute paths)
        // Use AppContext.BaseDirectory (executable location) instead of Directory.GetCurrentDirectory()
        _baseDirectory = Path.IsPathRooted(_settings.RulesDirectory)
            ? _settings.RulesDirectory
            : Path.Combine(AppContext.BaseDirectory, _settings.RulesDirectory);
    }

    /// <summary>
    /// Loads and merges all active rule sets.
    /// </summary>
    public TextCleaningRuleSet LoadRules()
    {
        if (_cachedRules != null)
        {
            return _cachedRules;
        }

        _logger.LogInformation(
            "Loading text cleaning rules from {Directory}. Active rule sets: {ActiveRuleSets}",
            _baseDirectory,
            string.Join(", ", _settings.ActiveRuleSets));

        if (!Directory.Exists(_baseDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Text cleaning rules directory not found: {_baseDirectory}");
        }

        var allRules = new List<TextCleaningRules>();

        foreach (var ruleSetName in _settings.ActiveRuleSets)
        {
            var filePath = Path.Combine(_baseDirectory, $"{ruleSetName}.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning(
                    "Rule set file not found: {FilePath}. Skipping.",
                    filePath);
                continue;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var rules = JsonSerializer.Deserialize<TextCleaningRules>(json, JsonOptions);

                if (rules == null)
                {
                    _logger.LogWarning("Failed to deserialize rules from {FilePath}", filePath);
                    continue;
                }

                if (!rules.Enabled)
                {
                    _logger.LogInformation("Rule set '{RuleSetName}' is disabled, skipping", ruleSetName);
                    continue;
                }

                allRules.Add(rules);

                _logger.LogInformation(
                    "Loaded rule set '{RuleSetName}': {Description}",
                    rules.Name,
                    rules.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rule set from {FilePath}", filePath);
                throw;
            }
        }

        if (allRules.Count == 0)
        {
            throw new InvalidOperationException("No text cleaning rules loaded");
        }

        // Merge all rules into a single rule set
        _cachedRules = MergeRules(allRules);

        _logger.LogInformation(
            "Successfully loaded and merged {Count} rule sets. " +
            "Total artifacts: {ArtifactCount}, Total patterns: {PatternCount}",
            allRules.Count,
            _cachedRules.FormArtifacts.Count,
            _cachedRules.FormFieldPatterns.Count +
            _cachedRules.SignaturePatterns.Count +
            _cachedRules.DateFieldPatterns.Count);

        return _cachedRules;
    }

    /// <summary>
    /// Merges multiple rule sets into a single combined rule set.
    /// Later rules override earlier rules for conflicts.
    /// </summary>
    private TextCleaningRuleSet MergeRules(List<TextCleaningRules> rulesList)
    {
        var merged = new TextCleaningRuleSet();

        foreach (var rules in rulesList)
        {
            // Merge form artifacts (union)
            merged.FormArtifacts.AddRange(rules.FormArtifacts);

            // Merge patterns (union)
            merged.FormFieldPatterns.AddRange(rules.FormFieldPatterns);
            merged.SignaturePatterns.AddRange(rules.SignaturePatterns);
            merged.DateFieldPatterns.AddRange(rules.DateFieldPatterns);
            merged.HeaderFooterPatterns.AddRange(rules.HeaderFooterPatterns);

            // Merge strategy configs (later wins)
            if (rules.WordSpacing != null)
            {
                merged.WordSpacing = rules.WordSpacing;
            }

            if (rules.Strategies != null)
            {
                if (rules.Strategies.UnicodeNormalization != null)
                    merged.UnicodeNormalization = rules.Strategies.UnicodeNormalization;

                if (rules.Strategies.WhitespaceNormalization != null)
                    merged.WhitespaceNormalization = rules.Strategies.WhitespaceNormalization;

                if (rules.Strategies.TableFormatting != null)
                    merged.TableFormatting = rules.Strategies.TableFormatting;

                if (rules.Strategies.RepetitiveContent != null)
                    merged.RepetitiveContent = rules.Strategies.RepetitiveContent;
            }

            if (rules.RepetitiveContent != null)
            {
                merged.RepetitiveContent = rules.RepetitiveContent;
            }
        }

        // Remove duplicates
        merged.FormArtifacts = merged.FormArtifacts.Distinct().ToList();
        merged.FormFieldPatterns = merged.FormFieldPatterns.Distinct().ToList();
        merged.SignaturePatterns = merged.SignaturePatterns.Distinct().ToList();
        merged.DateFieldPatterns = merged.DateFieldPatterns.Distinct().ToList();
        merged.HeaderFooterPatterns = merged.HeaderFooterPatterns.Distinct().ToList();

        return merged;
    }

    /// <summary>
    /// Clears cached rules, forcing a reload on next access.
    /// </summary>
    public void ClearCache()
    {
        _cachedRules = null;
        _logger.LogInformation("Text cleaning rules cache cleared");
    }
}

/// <summary>
/// Represents a merged set of text cleaning rules ready to use.
/// </summary>
public class TextCleaningRuleSet
{
    public List<string> FormArtifacts { get; set; } = new();
    public List<string> FormFieldPatterns { get; set; } = new();
    public List<string> SignaturePatterns { get; set; } = new();
    public List<string> DateFieldPatterns { get; set; } = new();
    public List<string> HeaderFooterPatterns { get; set; } = new();

    public WordSpacingConfig? WordSpacing { get; set; }
    public UnicodeNormalizationConfig? UnicodeNormalization { get; set; }
    public WhitespaceNormalizationConfig? WhitespaceNormalization { get; set; }
    public TableFormattingConfig? TableFormatting { get; set; }
    public RepetitiveContentConfig? RepetitiveContent { get; set; }
}
