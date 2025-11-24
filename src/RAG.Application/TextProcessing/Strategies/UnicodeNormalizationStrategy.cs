using System.Text;
using RAG.Application.Interfaces;

namespace RAG.Application.TextProcessing.Strategies;

/// <summary>
/// Normalizes Unicode characters (spaces, quotes, dashes) to standard forms.
/// Language-agnostic strategy.
/// </summary>
public class UnicodeNormalizationStrategy : ITextCleaningStrategy
{
    public string Name => "UnicodeNormalization";

    public string Apply(string text)
    {
        // Normalize to NFC form (canonical composition)
        text = text.Normalize(NormalizationForm.FormC);

        // Replace various types of spaces with regular space
        text = text.Replace('\u00A0', ' '); // Non-breaking space
        text = text.Replace('\u2003', ' '); // Em space
        text = text.Replace('\u2002', ' '); // En space
        text = text.Replace('\u2009', ' '); // Thin space
        text = text.Replace('\u200B', ' '); // Zero-width space

        // Normalize quotes to straight quotes
        text = text.Replace('\u2018', '\''); // Left single quote
        text = text.Replace('\u2019', '\''); // Right single quote
        text = text.Replace('\u201C', '"');  // Left double quote
        text = text.Replace('\u201D', '"');  // Right double quote

        // Normalize dashes to hyphen-minus
        text = text.Replace('\u2013', '-'); // En dash
        text = text.Replace('\u2014', '-'); // Em dash

        return text;
    }
}
