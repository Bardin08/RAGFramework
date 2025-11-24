using System.Text.RegularExpressions;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Removes form artifacts based on loaded rule patterns.
/// Supports language-specific and document-type-specific configurations.
/// </summary>
public class FormArtifactRemovalStrategy : ITextCleaningStrategy
{
    private readonly TextCleaningRulesLoader _rulesLoader;
    private readonly Lazy<List<Regex>> _compiledFormFieldPatterns;
    private readonly Lazy<List<Regex>> _compiledSignaturePatterns;
    private readonly Lazy<List<Regex>> _compiledDateFieldPatterns;
    private readonly Lazy<TextCleaningRuleSet> _rules;

    public string Name => "FormArtifactRemoval";

    public FormArtifactRemovalStrategy(TextCleaningRulesLoader rulesLoader)
    {
        _rulesLoader = rulesLoader ?? throw new ArgumentNullException(nameof(rulesLoader));

        // Lazy-load and compile patterns for performance
        _rules = new Lazy<TextCleaningRuleSet>(() => _rulesLoader.LoadRules());

        _compiledFormFieldPatterns = new Lazy<List<Regex>>(() =>
            _rules.Value.FormFieldPatterns
                .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.Multiline))
                .ToList());

        _compiledSignaturePatterns = new Lazy<List<Regex>>(() =>
            _rules.Value.SignaturePatterns
                .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToList());

        _compiledDateFieldPatterns = new Lazy<List<Regex>>(() =>
            _rules.Value.DateFieldPatterns
                .Select(p => new Regex(p, RegexOptions.Compiled))
                .ToList());
    }

    public string Apply(string text)
    {
        // Remove date field patterns
        foreach (var pattern in _compiledDateFieldPatterns.Value)
        {
            text = pattern.Replace(text, "");
        }

        // Remove signature line patterns
        foreach (var pattern in _compiledSignaturePatterns.Value)
        {
            text = pattern.Replace(text, "");
        }

        // Remove form field lines
        foreach (var pattern in _compiledFormFieldPatterns.Value)
        {
            text = pattern.Replace(text, "\n");
        }

        // Remove excessive underscores (common in forms)
        text = Regex.Replace(text, @"_{3,}", "", RegexOptions.Compiled);

        // Remove configured form artifacts
        foreach (var artifact in _rules.Value.FormArtifacts)
        {
            text = text.Replace(artifact, "");
        }

        return text;
    }

    public bool IsApplicable(string text)
    {
        var rules = _rules.Value;
        return rules.FormFieldPatterns.Any() ||
               rules.SignaturePatterns.Any() ||
               rules.DateFieldPatterns.Any() ||
               rules.FormArtifacts.Any();
    }
}
