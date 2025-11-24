namespace RAG.Core.Configuration;

/// <summary>
/// Application settings for text cleaning (not the rules themselves).
/// Rules are loaded from separate configuration files.
/// </summary>
public class TextCleaningSettings
{
    /// <summary>
    /// Enable text cleaning globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the directory containing text cleaning rule files.
    /// </summary>
    public string RulesDirectory { get; set; } = "config/text-cleaning";

    /// <summary>
    /// Active rule sets to load (e.g., ["base", "ukrainian", "academic-forms"]).
    /// </summary>
    public List<string> ActiveRuleSets { get; set; } = new();

    /// <summary>
    /// Default language for text cleaning.
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// Enable detailed logging of text cleaning operations.
    /// </summary>
    public bool DetailedLogging { get; set; } = false;

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RulesDirectory))
        {
            throw new InvalidOperationException("RulesDirectory must be specified");
        }

        if (ActiveRuleSets.Count == 0)
        {
            throw new InvalidOperationException("At least one rule set must be active");
        }
    }
}
