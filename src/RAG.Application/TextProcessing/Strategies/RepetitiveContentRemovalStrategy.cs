using System.Text;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Removes repetitive content (headers/footers) based on configurable thresholds.
/// Language-agnostic strategy.
/// </summary>
public class RepetitiveContentRemovalStrategy : ITextCleaningStrategy
{
    private readonly TextCleaningRulesLoader _rulesLoader;
    private readonly Lazy<TextCleaningRuleSet> _rules;

    public string Name => "RepetitiveContentRemoval";

    public RepetitiveContentRemovalStrategy(TextCleaningRulesLoader rulesLoader)
    {
        _rulesLoader = rulesLoader ?? throw new ArgumentNullException(nameof(rulesLoader));

        // Lazy-load rules
        _rules = new Lazy<TextCleaningRuleSet>(() => _rulesLoader.LoadRules());
    }

    public string Apply(string text)
    {
        var config = _rules.Value.RepetitiveContent;
        if (config == null)
        {
            return text;
        }

        var lines = text.Split('\n');
        var lineFrequency = new Dictionary<string, int>();

        // Count line occurrences (ignoring empty lines and lines below minimum length)
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length >= config.MinimumLineLength)
            {
                if (!lineFrequency.ContainsKey(trimmedLine))
                {
                    lineFrequency[trimmedLine] = 0;
                }
                lineFrequency[trimmedLine]++;
            }
        }

        // Find lines that appear >= threshold times (likely headers/footers)
        var repetitiveLines = lineFrequency
            .Where(kvp => kvp.Value >= config.Threshold)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        if (repetitiveLines.Count == 0)
        {
            return text;
        }

        // Keep first occurrence, remove subsequent ones
        var seenLines = new HashSet<string>();
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (repetitiveLines.Contains(trimmedLine))
            {
                if (!seenLines.Contains(trimmedLine))
                {
                    result.AppendLine(line);
                    seenLines.Add(trimmedLine);
                }
                // Skip subsequent occurrences
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    public bool IsApplicable(string text)
    {
        var config = _rules.Value.RepetitiveContent;
        return config != null && config.Enabled;
    }
}
