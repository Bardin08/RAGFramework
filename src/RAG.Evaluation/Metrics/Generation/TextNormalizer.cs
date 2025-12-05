using System.Text;
using System.Text.RegularExpressions;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Utility for normalizing text before comparison in evaluation metrics.
/// </summary>
public static partial class TextNormalizer
{
    /// <summary>
    /// Normalizes text for comparison: lowercase, remove punctuation, normalize whitespace.
    /// </summary>
    public static string Normalize(string text, bool removePunctuation = true, bool lowercase = true)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var result = text;

        // Lowercase
        if (lowercase)
            result = result.ToLowerInvariant();

        // Remove punctuation
        if (removePunctuation)
            result = PunctuationRegex().Replace(result, " ");

        // Normalize whitespace
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return result;
    }

    /// <summary>
    /// Tokenizes text into words using whitespace.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Tokenizes and normalizes text.
    /// </summary>
    public static IReadOnlyList<string> TokenizeNormalized(string text)
    {
        return Tokenize(Normalize(text));
    }

    /// <summary>
    /// Extracts n-grams from a list of tokens.
    /// </summary>
    public static IReadOnlyList<string> ExtractNGrams(IReadOnlyList<string> tokens, int n)
    {
        if (tokens.Count < n)
            return [];

        var ngrams = new List<string>(tokens.Count - n + 1);
        for (var i = 0; i <= tokens.Count - n; i++)
        {
            var ngram = string.Join(" ", tokens.Skip(i).Take(n));
            ngrams.Add(ngram);
        }

        return ngrams;
    }

    /// <summary>
    /// Computes Longest Common Subsequence length between two sequences.
    /// </summary>
    public static int LongestCommonSubsequence<T>(IReadOnlyList<T> seq1, IReadOnlyList<T> seq2) where T : IEquatable<T>
    {
        var m = seq1.Count;
        var n = seq2.Count;

        // Use 1D array optimization
        var dp = new int[n + 1];

        for (var i = 1; i <= m; i++)
        {
            var prev = 0;
            for (var j = 1; j <= n; j++)
            {
                var temp = dp[j];
                if (seq1[i - 1].Equals(seq2[j - 1]))
                    dp[j] = prev + 1;
                else
                    dp[j] = Math.Max(dp[j], dp[j - 1]);
                prev = temp;
            }
        }

        return dp[n];
    }

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
