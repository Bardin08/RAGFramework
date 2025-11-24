using System.Text.RegularExpressions;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Cleans up table formatting (converts table separators to readable format).
/// Language-agnostic strategy.
/// </summary>
public class TableFormattingCleanupStrategy : ITextCleaningStrategy
{
    private readonly TextCleaningRulesLoader _rulesLoader;
    private readonly Lazy<TextCleaningRuleSet> _rules;
    private static readonly Regex TableSeparators = new(@"\s*\|\s*", RegexOptions.Compiled);

    public string Name => "TableFormattingCleanup";

    public TableFormattingCleanupStrategy(TextCleaningRulesLoader rulesLoader)
    {
        _rulesLoader = rulesLoader ?? throw new ArgumentNullException(nameof(rulesLoader));

        // Lazy-load rules
        _rules = new Lazy<TextCleaningRuleSet>(() => _rulesLoader.LoadRules());
    }

    public string Apply(string text)
    {
        var config = _rules.Value.TableFormatting;
        if (config == null || !config.ConvertSeparators)
        {
            return text;
        }

        // Replace table cell separators (|) with configured replacement (default: ", ")
        var replacement = string.IsNullOrEmpty(config.SeparatorReplacement)
            ? ", "
            : config.SeparatorReplacement;

        text = TableSeparators.Replace(text, replacement);

        return text;
    }

    public bool IsApplicable(string text)
    {
        var config = _rules.Value.TableFormatting;
        return config != null && config.Enabled && text.Contains('|');
    }
}
