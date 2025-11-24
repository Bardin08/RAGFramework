namespace RAG.Core.Configuration;

/// <summary>
/// Represents a complete set of text cleaning rules loaded from configuration files.
/// </summary>
public class TextCleaningRules
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? Language { get; set; }
    public string? DocumentType { get; set; }

    // Strategy-specific configurations
    public WordSpacingConfig? WordSpacing { get; set; }
    public StrategiesConfig? Strategies { get; set; }
    public RepetitiveContentConfig? RepetitiveContent { get; set; }

    // Pattern lists
    public List<string> FormArtifacts { get; set; } = new();
    public List<string> SignaturePatterns { get; set; } = new();
    public List<string> DateFieldPatterns { get; set; } = new();
    public List<string> FormFieldPatterns { get; set; } = new();
    public List<string> HeaderFooterPatterns { get; set; } = new();
}

/// <summary>
/// Word spacing configuration.
/// </summary>
public class WordSpacingConfig
{
    public bool Enabled { get; set; } = true;
    public string Pattern { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Strategy configuration for base rules.
/// </summary>
public class StrategiesConfig
{
    public UnicodeNormalizationConfig? UnicodeNormalization { get; set; }
    public WhitespaceNormalizationConfig? WhitespaceNormalization { get; set; }
    public TableFormattingConfig? TableFormatting { get; set; }
    public RepetitiveContentConfig? RepetitiveContent { get; set; }
}

public class UnicodeNormalizationConfig
{
    public bool Enabled { get; set; } = true;
    public bool NormalizeSpaces { get; set; } = true;
    public bool NormalizeQuotes { get; set; } = true;
    public bool NormalizeDashes { get; set; } = true;
}

public class WhitespaceNormalizationConfig
{
    public bool Enabled { get; set; } = true;
    public bool RemoveExcessiveSpaces { get; set; } = true;
    public bool RemoveExcessiveLineBreaks { get; set; } = true;
    public int MaxConsecutiveBlankLines { get; set; } = 2;
}

public class TableFormattingConfig
{
    public bool Enabled { get; set; } = true;
    public bool ConvertSeparators { get; set; } = true;
    public string SeparatorReplacement { get; set; } = ", ";
}

public class RepetitiveContentConfig
{
    public bool Enabled { get; set; } = true;
    public int Threshold { get; set; } = 3;
    public int MinimumLineLength { get; set; } = 20;
}
