using System.Text.RegularExpressions;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Normalizes whitespace (spaces, tabs, line breaks).
/// Language-agnostic strategy.
/// </summary>
public class WhitespaceNormalizationStrategy : ITextCleaningStrategy
{
    private static readonly Regex MultipleSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex MultipleBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    public string Name => "WhitespaceNormalization";

    public string Apply(string text)
    {
        // Replace tabs with spaces
        text = text.Replace('\t', ' ');

        // Replace multiple spaces with single space
        text = MultipleSpaces.Replace(text, " ");

        // Trim spaces at the end of lines
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        text = string.Join('\n', lines);

        // Replace multiple blank lines with double line break
        text = MultipleBlankLines.Replace(text, "\n\n");

        return text;
    }
}
