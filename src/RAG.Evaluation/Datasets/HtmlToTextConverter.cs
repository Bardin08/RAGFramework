using System.Text;
using System.Text.RegularExpressions;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Converts HTML content to clean plain text, suitable for Wikipedia passages.
/// </summary>
public class HtmlToTextConverter
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ScriptStyleRegex = new(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MultipleNewlinesRegex = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Converts HTML to clean plain text.
    /// </summary>
    /// <param name="html">The HTML content to convert.</param>
    /// <returns>Clean plain text without HTML tags.</returns>
    public string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = html;

        // Remove script and style tags with their content
        text = ScriptStyleRegex.Replace(text, string.Empty);

        // Replace common HTML entities
        text = ReplaceHtmlEntities(text);

        // Replace <br>, <p>, <div> tags with newlines
        text = ReplaceBlockElements(text);

        // Remove all remaining HTML tags
        text = HtmlTagRegex.Replace(text, string.Empty);

        // Normalize whitespace
        text = NormalizeWhitespace(text);

        // Remove excessive newlines
        text = MultipleNewlinesRegex.Replace(text, "\n\n");

        return text.Trim();
    }

    /// <summary>
    /// Replaces common HTML entities with their text equivalents.
    /// </summary>
    private static string ReplaceHtmlEntities(string text)
    {
        var entities = new Dictionary<string, string>
        {
            { "&nbsp;", " " },
            { "&lt;", "<" },
            { "&gt;", ">" },
            { "&amp;", "&" },
            { "&quot;", "\"" },
            { "&apos;", "'" },
            { "&#39;", "'" },
            { "&mdash;", "—" },
            { "&ndash;", "–" },
            { "&hellip;", "..." },
            { "&ldquo;", "\"" },
            { "&rdquo;", "\"" },
            { "&lsquo;", "'" },
            { "&rsquo;", "'" },
            { "&copy;", "©" },
            { "&reg;", "®" },
            { "&trade;", "™" }
        };

        foreach (var entity in entities)
        {
            text = text.Replace(entity.Key, entity.Value);
        }

        // Handle numeric entities (e.g., &#160;)
        text = Regex.Replace(text, @"&#(\d+);", match =>
        {
            if (int.TryParse(match.Groups[1].Value, out int code))
            {
                try
                {
                    return ((char)code).ToString();
                }
                catch
                {
                    return match.Value;
                }
            }
            return match.Value;
        });

        return text;
    }

    /// <summary>
    /// Replaces block-level HTML elements with newlines.
    /// </summary>
    private static string ReplaceBlockElements(string text)
    {
        var blockElements = new[]
        {
            "br", "p", "div", "h1", "h2", "h3", "h4", "h5", "h6",
            "li", "tr", "td", "th", "section", "article", "header", "footer"
        };

        foreach (var element in blockElements)
        {
            // Self-closing and opening tags
            text = Regex.Replace(
                text,
                $@"<{element}[^>]*>",
                "\n",
                RegexOptions.IgnoreCase);

            // Closing tags
            text = Regex.Replace(
                text,
                $@"</{element}>",
                "\n",
                RegexOptions.IgnoreCase);
        }

        return text;
    }

    /// <summary>
    /// Normalizes whitespace in text.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        var lines = text.Split('\n');
        var normalizedLines = new List<string>();

        foreach (var line in lines)
        {
            // Collapse multiple spaces into one
            var normalizedLine = WhitespaceRegex.Replace(line, " ").Trim();
            if (!string.IsNullOrWhiteSpace(normalizedLine))
            {
                normalizedLines.Add(normalizedLine);
            }
        }

        return string.Join("\n", normalizedLines);
    }

    /// <summary>
    /// Extracts a clean snippet from HTML around a specific text.
    /// </summary>
    /// <param name="html">The HTML content.</param>
    /// <param name="targetText">The text to find.</param>
    /// <param name="contextChars">Number of characters of context to include.</param>
    /// <returns>A clean text snippet with context around the target text.</returns>
    public string ExtractSnippet(string html, string targetText, int contextChars = 200)
    {
        var cleanText = Convert(html);

        if (string.IsNullOrWhiteSpace(cleanText) || string.IsNullOrWhiteSpace(targetText))
            return string.Empty;

        var index = cleanText.IndexOf(targetText, StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return string.Empty;

        var start = Math.Max(0, index - contextChars);
        var end = Math.Min(cleanText.Length, index + targetText.Length + contextChars);

        var snippet = cleanText.Substring(start, end - start);

        // Add ellipsis if we're not at the start/end
        if (start > 0)
            snippet = "..." + snippet;
        if (end < cleanText.Length)
            snippet = snippet + "...";

        return snippet.Trim();
    }
}
