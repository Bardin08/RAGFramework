using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Infrastructure.Chunking;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Chunking;

public class SlidingWindowChunkerTests
{
    private readonly Mock<ILogger<SlidingWindowChunker>> _loggerMock;
    private readonly ChunkingOptions _defaultOptions;

    public SlidingWindowChunkerTests()
    {
        _loggerMock = new Mock<ILogger<SlidingWindowChunker>>();
        _defaultOptions = new ChunkingOptions { ChunkSize = 512, OverlapSize = 128 };
    }

    private SlidingWindowChunker CreateChunker(ChunkingOptions? options = null)
    {
        var optionsToUse = options ?? _defaultOptions;
        var optionsMock = new Mock<IOptions<ChunkingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(optionsToUse);
        return new SlidingWindowChunker(optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ChunkAsync_WithEmptyString_ReturnsEmptyList()
    {
        // Arrange
        var chunker = CreateChunker();
        var documentId = Guid.NewGuid();

        // Act
        var result = await chunker.ChunkAsync("", documentId);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ChunkAsync_WithNullString_ReturnsEmptyList()
    {
        // Arrange
        var chunker = CreateChunker();
        var documentId = Guid.NewGuid();

        // Act
        var result = await chunker.ChunkAsync(null!, documentId);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ChunkAsync_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange
        var chunker = CreateChunker();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await chunker.ChunkAsync("test text", Guid.Empty));
    }

    [Fact]
    public async Task ChunkAsync_WithTextShorterThanChunkSize_ReturnsSingleChunk()
    {
        // Arrange
        var chunker = CreateChunker();
        var documentId = Guid.NewGuid();
        var text = "This is a short text with only a few words.";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Text.ShouldBe(text);
        result[0].DocumentId.ShouldBe(documentId);
        result[0].StartIndex.ShouldBe(0);
        result[0].EndIndex.ShouldBe(text.Length);
        result[0].ChunkIndex.ShouldBe(0);
        result[0].Metadata.ShouldContainKey("TokenCount");
        result[0].Metadata.ShouldContainKey("ChunkingStrategy");
        result[0].Metadata["ChunkingStrategy"].ShouldBe("SlidingWindow");
    }

    [Fact]
    public async Task ChunkAsync_WithTextExactlyChunkSize_ReturnsSingleChunk()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 6 }; // ~20 words, ~5 words overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        // Exactly 20 words
        var text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBe(1);
        result[0].ChunkIndex.ShouldBe(0);
    }

    [Fact]
    public async Task ChunkAsync_WithLongText_ReturnsMultipleChunks()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 6 }; // ~20 words, ~5 words overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        // 50 words - should create multiple chunks
        var text = "word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 " +
                   "word11 word12 word13 word14 word15 word16 word17 word18 word19 word20 " +
                   "word21 word22 word23 word24 word25 word26 word27 word28 word29 word30 " +
                   "word31 word32 word33 word34 word35 word36 word37 word38 word39 word40 " +
                   "word41 word42 word43 word44 word45 word46 word47 word48 word49 word50";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(1);
        result[0].ChunkIndex.ShouldBe(0);
        result[1].ChunkIndex.ShouldBe(1);
        if (result.Count > 2)
        {
            result[2].ChunkIndex.ShouldBe(2);
        }
    }

    [Fact]
    public async Task ChunkAsync_VerifiesOverlapCorrectness()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 6 }; // ~20 words, ~5 words overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = "word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 " +
                   "word11 word12 word13 word14 word15 word16 word17 word18 word19 word20 " +
                   "word21 word22 word23 word24 word25 word26 word27 word28 word29 word30";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(1);

        // Verify that chunks overlap: text from end of chunk[i] should appear in start of chunk[i+1]
        for (int i = 0; i < result.Count - 1; i++)
        {
            var currentChunkEndWords = result[i].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(3).ToArray();
            var nextChunkStartWords = result[i + 1].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5).ToArray();

            // At least some overlap should exist
            var hasOverlap = currentChunkEndWords.Any(word => nextChunkStartWords.Contains(word));
            hasOverlap.ShouldBeTrue($"Chunk {i} and {i + 1} should have overlapping words");
        }
    }

    [Fact]
    public async Task ChunkAsync_VerifiesNoDataLoss()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 39, OverlapSize = 13 }; // ~30 words, ~10 words overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = "The quick brown fox jumps over the lazy dog. " +
                   "This is a test sentence with multiple words to verify chunking. " +
                   "We want to ensure that all text is preserved across chunks without any data loss. " +
                   "This text should be split into multiple overlapping chunks for testing purposes.";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(0);

        // Reconstruct text from chunks (removing overlap by checking character positions)
        var reconstructedText = string.Join("", result.Select(c => c.Text));

        // All words from original should appear in reconstructed (allowing for overlap)
        var originalWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var reconstructedWords = reconstructedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in originalWords)
        {
            reconstructedWords.ShouldContain(word, $"Word '{word}' should be present in chunked text");
        }
    }

    [Fact]
    public async Task ChunkAsync_VerifiesCharacterPositionTracking()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 6 };
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = "word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 " +
                   "word11 word12 word13 word14 word15 word16 word17 word18 word19 word20";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        foreach (var chunk in result)
        {
            // Verify that chunk.Text matches the substring at the specified positions
            var expectedText = text.Substring(chunk.StartIndex, chunk.EndIndex - chunk.StartIndex);
            chunk.Text.Trim().ShouldBe(expectedText.Trim(),
                $"Chunk {chunk.ChunkIndex} text should match substring from original text");
        }
    }

    [Fact]
    public async Task ChunkAsync_VerifiesSequentialChunkIndex()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 6 };
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = string.Join(" ", Enumerable.Range(1, 60).Select(i => $"word{i}"));

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(1);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].ChunkIndex.ShouldBe(i, $"Chunk at position {i} should have ChunkIndex {i}");
        }
    }

    [Fact]
    public async Task ChunkAsync_VerifiesMetadataPresence()
    {
        // Arrange
        var chunker = CreateChunker();
        var documentId = Guid.NewGuid();
        var text = "This is a test sentence with several words to chunk.";

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(0);
        foreach (var chunk in result)
        {
            chunk.Metadata.ShouldNotBeNull();
            chunk.Metadata.ShouldContainKey("TokenCount");
            chunk.Metadata.ShouldContainKey("ChunkingStrategy");
            chunk.Metadata.ShouldContainKey("WordCount");
            chunk.Metadata["ChunkingStrategy"].ShouldBe("SlidingWindow");
            ((int)chunk.Metadata["TokenCount"]).ShouldBeGreaterThan(0);
            ((int)chunk.Metadata["WordCount"]).ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ChunkAsync_WithDifferentConfiguration_RespectsSettings()
    {
        // Arrange - smaller chunk size
        var options = new ChunkingOptions { ChunkSize = 13, OverlapSize = 3 }; // ~10 words, ~2 words overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = string.Join(" ", Enumerable.Range(1, 30).Select(i => $"word{i}"));

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        // With smaller chunk size, we should get more chunks
        result.Count.ShouldBeGreaterThan(2);
    }

    [Fact]
    public async Task ChunkAsync_WithZeroOverlap_CreatesNonOverlappingChunks()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 26, OverlapSize = 0 }; // ~20 words, no overlap
        var chunker = CreateChunker(options);
        var documentId = Guid.NewGuid();
        var text = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"word{i}"));

        // Act
        var result = await chunker.ChunkAsync(text, documentId);

        // Assert
        result.Count.ShouldBeGreaterThan(1);

        // Verify no overlap: end of chunk[i] should not contain words from start of chunk[i+1]
        for (int i = 0; i < result.Count - 1; i++)
        {
            // EndIndex of current chunk should be <= StartIndex of next chunk (allowing whitespace)
            result[i].EndIndex.ShouldBeLessThanOrEqualTo(result[i + 1].StartIndex + 5,
                "Non-overlapping chunks should not have significant position overlap");
        }
    }

    [Fact]
    public async Task ChunkAsync_WithLargeText_HandlesPerformance()
    {
        // Arrange
        var chunker = CreateChunker();
        var documentId = Guid.NewGuid();
        // Generate text with ~5000 words
        var text = string.Join(" ", Enumerable.Range(1, 5000).Select(i => $"word{i}"));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await chunker.ChunkAsync(text, documentId);
        stopwatch.Stop();

        // Assert
        result.Count.ShouldBeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000, "Chunking 5000 words should take less than 1 second");
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsException()
    {
        // Arrange
        var invalidOptions = new ChunkingOptions { ChunkSize = 0, OverlapSize = 0 };
        var optionsMock = new Mock<IOptions<ChunkingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(invalidOptions);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new SlidingWindowChunker(optionsMock.Object, _loggerMock.Object));
    }
}
