using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;

namespace RAG.Infrastructure.Chunking;

/// <summary>
/// Implements a sliding window chunking strategy that splits text into overlapping chunks
/// using word-based tokenization.
/// </summary>
public class SlidingWindowChunker : IChunkingStrategy
{
    private readonly ChunkingOptions _options;
    private readonly ILogger<SlidingWindowChunker> _logger;
    private const double TokenToWordRatio = 1.3; // Conservative estimate: 1 word ≈ 1.3 tokens

    public SlidingWindowChunker(IOptions<ChunkingOptions> options, ILogger<SlidingWindowChunker> logger)
    {
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<DocumentChunk>> ChunkAsync(string text, Guid documentId, CancellationToken cancellationToken = default)
    {
        // Handle edge case: null or empty text
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogDebug("Empty or null text provided, returning empty chunk list");
            return new List<DocumentChunk>();
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("DocumentId cannot be empty", nameof(documentId));
        }

        // Tokenize text into words
        var words = TokenizeText(text);
        var totalWords = words.Count;

        _logger.LogInformation(
            "Chunking text: {TextLength} chars, {WordCount} words → estimated {TokenCount} tokens (ChunkSize={ChunkSize}, Overlap={Overlap})",
            text.Length, totalWords, (int)(totalWords * TokenToWordRatio), _options.ChunkSize, _options.OverlapSize);

        // Calculate chunk size in words (approximate tokens)
        var chunkSizeInWords = (int)(_options.ChunkSize / TokenToWordRatio);
        var overlapSizeInWords = (int)(_options.OverlapSize / TokenToWordRatio);

        // Handle edge case: text shorter than chunk size
        if (totalWords <= chunkSizeInWords)
        {
            _logger.LogDebug("Text is shorter than chunk size, creating single chunk");
            var singleChunk = new DocumentChunk(
                id: Guid.NewGuid(),
                documentId: documentId,
                text: text,
                startIndex: 0,
                endIndex: text.Length,
                chunkIndex: 0,
                metadata: new Dictionary<string, object>
                {
                    { "TokenCount", (int)(totalWords * TokenToWordRatio) },
                    { "ChunkingStrategy", "SlidingWindow" },
                    { "WordCount", totalWords }
                });
            return new List<DocumentChunk> { singleChunk };
        }

        // Create chunks with sliding window
        var chunks = new List<DocumentChunk>();
        int chunkIndex = 0;
        int startWordIndex = 0;

        while (startWordIndex < totalWords)
        {
            // Calculate end word index for this chunk
            int endWordIndex = Math.Min(startWordIndex + chunkSizeInWords, totalWords);

            // Extract words for this chunk
            var chunkWords = words.Skip(startWordIndex).Take(endWordIndex - startWordIndex).ToList();

            // Map word indices to character positions
            var (startCharIndex, endCharIndex) = MapWordsToCharacterPositions(text, words, startWordIndex, endWordIndex);

            // Extract chunk text
            var chunkText = text.Substring(startCharIndex, endCharIndex - startCharIndex);

            // Estimate token count for this chunk
            var estimatedTokens = (int)(chunkWords.Count * TokenToWordRatio);

            // Create chunk
            var chunk = new DocumentChunk(
                id: Guid.NewGuid(),
                documentId: documentId,
                text: chunkText,
                startIndex: startCharIndex,
                endIndex: endCharIndex,
                chunkIndex: chunkIndex,
                metadata: new Dictionary<string, object>
                {
                    { "TokenCount", estimatedTokens },
                    { "ChunkingStrategy", "SlidingWindow" },
                    { "WordCount", chunkWords.Count }
                });

            chunks.Add(chunk);
            _logger.LogDebug(
                "Created chunk {ChunkIndex}: chars [{StartIndex}..{EndIndex}], {WordCount} words, ~{TokenCount} tokens",
                chunkIndex, startCharIndex, endCharIndex, chunkWords.Count, estimatedTokens);

            chunkIndex++;

            // Slide window: move start forward by (chunkSize - overlap)
            startWordIndex += chunkSizeInWords - overlapSizeInWords;

            // Prevent infinite loop: if we've processed all text, break
            if (endWordIndex >= totalWords)
                break;
        }

        _logger.LogInformation(
            "Chunking complete: {ChunkCount} chunks created from {TextLength} chars",
            chunks.Count, text.Length);

        return await Task.FromResult(chunks);
    }

    /// <summary>
    /// Tokenizes text into words using whitespace and punctuation as delimiters.
    /// </summary>
    private List<string> TokenizeText(string text)
    {
        // Split by whitespace and common punctuation while preserving structure
        var separators = new[] { ' ', '\t', '\n', '\r' };
        var words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries).ToList();
        return words;
    }

    /// <summary>
    /// Maps word indices to character positions in the original text.
    /// </summary>
    private (int startCharIndex, int endCharIndex) MapWordsToCharacterPositions(
        string text, List<string> words, int startWordIndex, int endWordIndex)
    {
        int startCharIndex = 0;
        int currentWordIndex = 0;
        int currentCharIndex = 0;

        // Find start character position
        while (currentWordIndex < startWordIndex && currentCharIndex < text.Length)
        {
            if (char.IsWhiteSpace(text[currentCharIndex]))
            {
                currentCharIndex++;
            }
            else
            {
                // Skip word
                while (currentCharIndex < text.Length && !char.IsWhiteSpace(text[currentCharIndex]))
                {
                    currentCharIndex++;
                }
                currentWordIndex++;
            }
        }

        // Skip leading whitespace for start of chunk
        while (currentCharIndex < text.Length && char.IsWhiteSpace(text[currentCharIndex]))
        {
            currentCharIndex++;
        }

        startCharIndex = currentCharIndex;

        // Find end character position
        while (currentWordIndex < endWordIndex && currentCharIndex < text.Length)
        {
            if (char.IsWhiteSpace(text[currentCharIndex]))
            {
                currentCharIndex++;
            }
            else
            {
                // Skip word
                while (currentCharIndex < text.Length && !char.IsWhiteSpace(text[currentCharIndex]))
                {
                    currentCharIndex++;
                }
                currentWordIndex++;
            }
        }

        // Include trailing whitespace up to next word or end of text
        while (currentCharIndex < text.Length && char.IsWhiteSpace(text[currentCharIndex]))
        {
            currentCharIndex++;
            // Stop at first non-whitespace to avoid including next chunk's content
            if (currentCharIndex < text.Length && !char.IsWhiteSpace(text[currentCharIndex]))
            {
                break;
            }
        }

        int endCharIndex = currentCharIndex;

        return (startCharIndex, endCharIndex);
    }
}
