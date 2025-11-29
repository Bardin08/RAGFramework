using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Core.Domain;

namespace RAG.Application.Services;

/// <summary>
/// Assembles formatted context from retrieval results for LLM input,
/// respecting token limits and applying relevance filtering and deduplication.
/// </summary>
public class ContextAssembler : IContextAssembler
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ContextAssemblyConfig _config;
    private readonly ILogger<ContextAssembler> _logger;

    public ContextAssembler(
        ITokenCounter tokenCounter,
        IOptions<ContextAssemblyConfig> config,
        ILogger<ContextAssembler> logger)
    {
        _tokenCounter = tokenCounter;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Assembles formatted context from retrieval results.
    /// AC#2: Sorts results by score, adds documents until maxTokens limit,truncates last if necessary.
    /// AC#4: Formats with source references: [Source X: file.pdf]
    /// AC#5: Performs deduplication of identical chunks.
    /// AC#6: Filters results below minScore threshold.
    /// </summary>
    public string AssembleContext(List<RetrievalResult> results, int? maxTokens = null)
    {
        if (results == null || results.Count == 0)
        {
            _logger.LogWarning("AssembleContext called with null or empty results");
            return string.Empty;
        }

        var tokenLimit = maxTokens ?? _config.MaxTokens;
        var safeTokenLimit = (int)(tokenLimit * 0.9); // AC#3: 10% safety buffer

        _logger.LogInformation("Assembling context for {Count} results, token limit: {Limit} (safe: {Safe})",
            results.Count, tokenLimit, safeTokenLimit);

        // AC#6: Filter by relevance threshold
        var relevantResults = results
            .Where(r => r.Score >= _config.MinScore)
            .OrderByDescending(r => r.Score) // AC#2: Sort by score descending
            .ToList();

        // Ensure at least 1 result remains even if below threshold
        if (relevantResults.Count == 0 && results.Count > 0)
        {
            relevantResults = results.OrderByDescending(r => r.Score).Take(1).ToList();
            _logger.LogWarning("All results below minScore {MinScore}, including highest scored result anyway",
                _config.MinScore);
        }
        else
        {
            _logger.LogInformation("Filtered {Total} results to {Relevant} relevant (score >= {MinScore})",
                results.Count, relevantResults.Count, _config.MinScore);
        }

        // AC#5: Deduplicate
        var deduplicated = _config.EnableDeduplication
            ? DeduplicateResults(relevantResults)
            : relevantResults;

        // AC#2, AC#4: Assemble context with token limit and source formatting
        return AssembleContextWithTokenLimit(deduplicated, safeTokenLimit);
    }

    /// <summary>
    /// Assembles context string with token limit enforcement.
    /// </summary>
    private string AssembleContextWithTokenLimit(List<RetrievalResult> results, int safeTokenLimit)
    {
        var contextBuilder = new StringBuilder();
        var currentTokens = 0;
        var sourceIndex = 1;

        foreach (var result in results)
        {
            // AC#4: Format with source reference
            var chunkHeader = $"[Source {sourceIndex}: {result.Source}]\n";
            var chunkText = result.Text;
            var chunkFooter = "\n\n";

            var fullChunk = chunkHeader + chunkText + chunkFooter;
            var chunkTokens = _tokenCounter.CountTokens(fullChunk);

            if (currentTokens + chunkTokens <= safeTokenLimit)
            {
                // Add entire chunk
                contextBuilder.Append(fullChunk);
                currentTokens += chunkTokens;
                sourceIndex++;
            }
            else
            {
                // AC#2: Truncate last document if necessary
                var remainingTokens = safeTokenLimit - currentTokens;

                if (remainingTokens > 20) // Only add if meaningful space remains
                {
                    // Estimate header + footer overhead (~10-15 tokens typically)
                    // Reserve this space for formatting, use rest for content
                    var estimatedOverhead = Math.Min(remainingTokens / 4, 15);
                    var availableForContent = remainingTokens - estimatedOverhead;

                    var truncatedText = TruncateToTokenLimit(chunkText, availableForContent);
                    var truncatedChunk = chunkHeader + truncatedText + "...\n\n";
                    contextBuilder.Append(truncatedChunk);

                    var truncatedTokens = _tokenCounter.CountTokens(truncatedChunk);
                    currentTokens += truncatedTokens;
                    sourceIndex++;

                    _logger.LogWarning("Truncated last chunk from {Original} to {Actual} tokens to fit limit",
                        chunkTokens, truncatedTokens);
                }
                break;
            }
        }

        _logger.LogInformation("Assembled context: {Chunks} chunks, {Tokens} tokens",
            sourceIndex - 1, currentTokens);

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Deduplicates results by removing identical text chunks.
    /// AC#5: If similarity > 0.9 (exact match here), keep chunk with higher score.
    /// </summary>
    private List<RetrievalResult> DeduplicateResults(List<RetrievalResult> results)
    {
        var deduplicated = new List<RetrievalResult>();
        var seenTexts = new HashSet<string>();

        foreach (var result in results)
        {
            // Simple exact match deduplication
            // Future enhancement: implement Jaccard/Levenshtein for fuzzy matching
            if (!seenTexts.Contains(result.Text))
            {
                deduplicated.Add(result);
                seenTexts.Add(result.Text);
            }
            else
            {
                _logger.LogDebug("Deduplicated chunk from {Source} (score: {Score})",
                    result.Source, result.Score);
            }
        }

        if (deduplicated.Count < results.Count)
        {
            _logger.LogInformation("Deduplicated {Original} → {Deduplicated} results",
                results.Count, deduplicated.Count);
        }

        return deduplicated;
    }

    /// <summary>
    /// Truncates text to fit within token limit.
    /// Uses approximate character-based truncation.
    /// </summary>
    private string TruncateToTokenLimit(string text, int maxTokens)
    {
        // Approximate: 1 token ≈ 4 characters
        var maxChars = maxTokens * 4;
        if (text.Length <= maxChars)
            return text;

        return text.Substring(0, maxChars);
    }
}
