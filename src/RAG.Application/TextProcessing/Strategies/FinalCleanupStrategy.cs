using System.Text.RegularExpressions;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Final cleanup pass - trimming, line break normalization.
/// Language-agnostic strategy.
/// </summary>
public class FinalCleanupStrategy : ITextCleaningStrategy
{
    public string Name => "FinalCleanup";

    public string Apply(string text)
    {
        // Remove leading/trailing whitespace
        text = text.Trim();

        // Ensure single line break between paragraphs
        text = Regex.Replace(text, @"\n\s*\n", "\n\n", RegexOptions.Compiled);

        // Filter out excessive empty lines while preserving paragraph breaks
        var lines = text.Split('\n');
        var cleanedLines = new List<string>();
        bool lastWasEmpty = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!lastWasEmpty)
                {
                    cleanedLines.Add("");
                    lastWasEmpty = true;
                }
            }
            else
            {
                cleanedLines.Add(trimmed);
                lastWasEmpty = false;
            }
        }

        return string.Join('\n', cleanedLines);
    }
}
