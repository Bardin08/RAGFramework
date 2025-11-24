using System.Text.RegularExpressions;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Fixes word spacing issues using configurable patterns.
/// Can be customized for different languages and scripts.
/// </summary>
public class WordSpacingFixStrategy : ITextCleaningStrategy
{
    private readonly TextCleaningRulesLoader _rulesLoader;
    private readonly Lazy<Regex?> _wordBoundaryPattern;
    private readonly Lazy<TextCleaningRuleSet> _rules;

    public string Name => "WordSpacingFix";

    public WordSpacingFixStrategy(TextCleaningRulesLoader rulesLoader)
    {
        _rulesLoader = rulesLoader ?? throw new ArgumentNullException(nameof(rulesLoader));

        // Lazy-load rules and compile pattern for performance
        _rules = new Lazy<TextCleaningRuleSet>(() => _rulesLoader.LoadRules());

        _wordBoundaryPattern = new Lazy<Regex?>(() =>
        {
            var config = _rules.Value.WordSpacing;
            if (config != null && config.Enabled && !string.IsNullOrWhiteSpace(config.Pattern))
            {
                return new Regex(config.Pattern, RegexOptions.Compiled);
            }
            return null;
        });
    }

    public string Apply(string text)
    {
        if (_wordBoundaryPattern.Value != null)
        {
            // Apply configured word boundary pattern
            text = _wordBoundaryPattern.Value.Replace(text, "$1 $2");
        }

        // Fix spacing around punctuation (language-agnostic)
        // Remove spaces before punctuation
        text = Regex.Replace(text, @"\s+([,;:.!?])", "$1", RegexOptions.Compiled);

        // Add space after punctuation if followed by letter/digit
        text = Regex.Replace(text, @"([,;:.!?])([A-Za-z0-9\u0400-\u04FF\u0600-\u06FF\u4E00-\u9FFF])", "$1 $2", RegexOptions.Compiled);

        return text;
    }

    public bool IsApplicable(string text)
    {
        var config = _rules.Value.WordSpacing;
        return config != null && config.Enabled;
    }
}
