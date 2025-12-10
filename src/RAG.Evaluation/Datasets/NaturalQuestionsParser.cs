using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Parses the Natural Questions dataset from JSONL format.
/// </summary>
public class NaturalQuestionsParser
{
    private readonly ILogger<NaturalQuestionsParser> _logger;

    public NaturalQuestionsParser(ILogger<NaturalQuestionsParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses a Natural Questions JSONL file.
    /// </summary>
    /// <param name="filePath">Path to the JSONL file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parsed Natural Questions entries.</returns>
    public async Task<List<NaturalQuestionsEntry>> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing Natural Questions dataset from: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Natural Questions file not found: {filePath}");
        }

        var entries = new List<NaturalQuestionsEntry>();
        var lineNumber = 0;
        var errorCount = 0;

        try
        {
            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = ParseLine(line, lineNumber);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException ex)
                {
                    errorCount++;
                    _logger.LogWarning(
                        ex,
                        "Failed to parse line {LineNumber} in Natural Questions file",
                        lineNumber);
                }
            }

            _logger.LogInformation(
                "Parsed {EntryCount} entries from Natural Questions dataset. " +
                "Total lines: {LineCount}, Errors: {ErrorCount}",
                entries.Count, lineNumber, errorCount);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to parse Natural Questions file at line {LineNumber}",
                lineNumber);
            throw;
        }
    }

    /// <summary>
    /// Parses a single JSONL line.
    /// </summary>
    private NaturalQuestionsEntry? ParseLine(string line, int lineNumber)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        var raw = JsonSerializer.Deserialize<NaturalQuestionsRaw>(line, options);

        if (raw == null)
        {
            _logger.LogWarning("Line {LineNumber}: Null entry after deserialization", lineNumber);
            return null;
        }

        // Extract question text
        var questionText = raw.question_text;
        if (string.IsNullOrWhiteSpace(questionText))
        {
            _logger.LogWarning("Line {LineNumber}: Missing question_text", lineNumber);
            return null;
        }

        // Extract document URL
        var documentUrl = raw.document_url ?? string.Empty;

        // Extract document HTML
        var documentHtml = raw.document_html ?? string.Empty;

        // Extract document title
        var documentTitle = raw.document_title ?? string.Empty;

        // Extract short answers
        var shortAnswers = new List<ShortAnswer>();
        if (raw.annotations != null && raw.annotations.Count > 0)
        {
            foreach (var annotation in raw.annotations)
            {
                if (annotation.short_answers != null)
                {
                    foreach (var shortAnswer in annotation.short_answers)
                    {
                        if (shortAnswer.start_token.HasValue &&
                            shortAnswer.end_token.HasValue &&
                            raw.document_tokens != null)
                        {
                            // Extract text from tokens
                            var text = ExtractTextFromTokens(
                                raw.document_tokens,
                                shortAnswer.start_token.Value,
                                shortAnswer.end_token.Value);

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                shortAnswers.Add(new ShortAnswer
                                {
                                    Text = text,
                                    StartToken = shortAnswer.start_token.Value,
                                    EndToken = shortAnswer.end_token.Value
                                });
                            }
                        }
                    }
                }
            }
        }

        // Extract long answer
        LongAnswer? longAnswer = null;
        if (raw.annotations != null && raw.annotations.Count > 0)
        {
            var annotation = raw.annotations[0];
            if (annotation.long_answer != null &&
                annotation.long_answer.start_token.HasValue &&
                annotation.long_answer.end_token.HasValue &&
                raw.document_tokens != null)
            {
                var text = ExtractTextFromTokens(
                    raw.document_tokens,
                    annotation.long_answer.start_token.Value,
                    annotation.long_answer.end_token.Value);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    longAnswer = new LongAnswer
                    {
                        Text = text,
                        StartToken = annotation.long_answer.start_token.Value,
                        EndToken = annotation.long_answer.end_token.Value
                    };
                }
            }
        }

        // Create entry ID from question hash
        var entryId = $"nq-{Math.Abs(questionText.GetHashCode())}";

        return new NaturalQuestionsEntry
        {
            Id = entryId,
            QuestionText = questionText,
            DocumentUrl = documentUrl,
            DocumentHtml = documentHtml,
            DocumentTitle = documentTitle,
            ShortAnswers = shortAnswers,
            LongAnswer = longAnswer,
            HasShortAnswer = shortAnswers.Count > 0
        };
    }

    /// <summary>
    /// Extracts text from document tokens within a range.
    /// </summary>
    private static string ExtractTextFromTokens(
        List<NaturalQuestionsToken> tokens,
        int startToken,
        int endToken)
    {
        if (startToken < 0 || endToken > tokens.Count || startToken >= endToken)
            return string.Empty;

        var textParts = new List<string>();
        for (int i = startToken; i < endToken && i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!string.IsNullOrWhiteSpace(token.token))
            {
                textParts.Add(token.token!);
            }
        }

        return string.Join(" ", textParts);
    }
}

/// <summary>
/// Represents a parsed Natural Questions entry.
/// </summary>
public class NaturalQuestionsEntry
{
    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The question text.
    /// </summary>
    public required string QuestionText { get; init; }

    /// <summary>
    /// URL of the source Wikipedia document.
    /// </summary>
    public required string DocumentUrl { get; init; }

    /// <summary>
    /// HTML content of the document.
    /// </summary>
    public required string DocumentHtml { get; init; }

    /// <summary>
    /// Title of the document.
    /// </summary>
    public required string DocumentTitle { get; init; }

    /// <summary>
    /// List of short answers (may be empty if no short answer exists).
    /// </summary>
    public required List<ShortAnswer> ShortAnswers { get; init; }

    /// <summary>
    /// Long answer paragraph (may be null).
    /// </summary>
    public LongAnswer? LongAnswer { get; init; }

    /// <summary>
    /// Whether this entry has at least one short answer.
    /// </summary>
    public bool HasShortAnswer { get; init; }
}

/// <summary>
/// Represents a short answer span.
/// </summary>
public class ShortAnswer
{
    /// <summary>
    /// The text of the short answer.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Start token index.
    /// </summary>
    public int StartToken { get; init; }

    /// <summary>
    /// End token index.
    /// </summary>
    public int EndToken { get; init; }
}

/// <summary>
/// Represents a long answer paragraph.
/// </summary>
public class LongAnswer
{
    /// <summary>
    /// The text of the long answer.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Start token index.
    /// </summary>
    public int StartToken { get; init; }

    /// <summary>
    /// End token index.
    /// </summary>
    public int EndToken { get; init; }
}

// Raw JSON deserialization classes
internal class NaturalQuestionsRaw
{
    public string? question_text { get; set; }
    public string? document_url { get; set; }
    public string? document_html { get; set; }
    public string? document_title { get; set; }
    public List<NaturalQuestionsToken>? document_tokens { get; set; }
    public List<NaturalQuestionsAnnotation>? annotations { get; set; }
}

internal class NaturalQuestionsToken
{
    public string? token { get; set; }
    public bool? html_token { get; set; }
}

internal class NaturalQuestionsAnnotation
{
    public List<NaturalQuestionsShortAnswer>? short_answers { get; set; }
    public NaturalQuestionsLongAnswer? long_answer { get; set; }
}

internal class NaturalQuestionsShortAnswer
{
    public int? start_token { get; set; }
    public int? end_token { get; set; }
}

internal class NaturalQuestionsLongAnswer
{
    public int? start_token { get; set; }
    public int? end_token { get; set; }
}
