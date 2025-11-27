using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Infrastructure.Chunking;
using Shouldly;

namespace RAG.Tests.Integration.Chunking;

public class ChunkingIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IChunkingStrategy _chunkingStrategy;

    public ChunkingIntegrationTests()
    {
        // Setup DI container with real services
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole());

        // Configure ChunkingOptions
        services.Configure<ChunkingOptions>(options =>
        {
            options.ChunkSize = 512;
            options.OverlapSize = 128;
        });

        // Register chunking strategy
        services.AddScoped<IChunkingStrategy, SlidingWindowChunker>();

        _serviceProvider = services.BuildServiceProvider();
        _chunkingStrategy = _serviceProvider.GetRequiredService<IChunkingStrategy>();
    }

    [Fact]
    public async Task ChunkAsync_WithRealTextFromFile_CreatesValidChunks()
    {
        // Arrange
        var sampleTextPath = Path.Combine(AppContext.BaseDirectory, "../../../Fixtures/sample.txt");
        var text = await File.ReadAllTextAsync(sampleTextPath);
        var documentId = Guid.NewGuid();

        // Act
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBeGreaterThan(0);

        // Verify all chunks have valid properties
        foreach (var chunk in chunks)
        {
            chunk.Id.ShouldNotBe(Guid.Empty);
            chunk.DocumentId.ShouldBe(documentId);
            chunk.Text.ShouldNotBeNullOrWhiteSpace();
            chunk.StartIndex.ShouldBeGreaterThanOrEqualTo(0);
            chunk.EndIndex.ShouldBeGreaterThan(chunk.StartIndex);
            chunk.Metadata.ShouldNotBeNull();
            chunk.Metadata.ShouldContainKey("TokenCount");
            chunk.Metadata.ShouldContainKey("ChunkingStrategy");
        }
    }

    [Fact]
    public async Task ChunkAsync_WithLongDocument_CreatesMultipleChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        // Generate a long document (>10,000 characters)
        var paragraphs = Enumerable.Range(1, 50).Select(i =>
            $"This is paragraph number {i}. It contains some text that simulates a real document. " +
            $"The quick brown fox jumps over the lazy dog. Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
            $"Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, " +
            $"quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.");
        var text = string.Join("\n\n", paragraphs);

        // Act
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        text.Length.ShouldBeGreaterThan(10000);

        // Verify chunk count is reasonable for text length
        var estimatedChunks = text.Length / 2000; // Rough estimate
        chunks.Count.ShouldBeGreaterThan(estimatedChunks / 2); // At least half of estimated
    }

    [Fact]
    public async Task ChunkAsync_VerifiesNoDataLossWithRealText()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var text = @"
Retrieval-Augmented Generation (RAG) is a powerful technique in natural language processing
that combines retrieval-based and generation-based approaches. The system first retrieves
relevant documents from a knowledge base, then uses these documents as context for generating
accurate and informed responses.

Key components of a RAG system include:
1. Document ingestion and indexing
2. Retrieval mechanism (BM25, dense vectors, hybrid)
3. Re-ranking for improved relevance
4. Generation using large language models
5. Evaluation and monitoring

This approach significantly reduces hallucinations and improves factual accuracy in AI responses.
The chunking strategy plays a critical role in maintaining context while optimizing retrieval performance.";

        // Act
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        chunks.Count.ShouldBeGreaterThan(0);

        // Verify all words from original text appear in chunks
        var originalWords = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var allChunkText = string.Join(" ", chunks.Select(c => c.Text));
        var chunkWords = allChunkText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in originalWords)
        {
            chunkWords.ShouldContain(word, $"Word '{word}' should be present in chunked text");
        }
    }

    [Fact]
    public async Task ChunkAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var text = @"
Special characters test: !@#$%^&*()_+-=[]{}|;:',.<>?/~`
Unicode characters: 你好世界, Привет мир, مرحبا بالعالم, שלום עולם
Newlines and tabs:
	Indented line 1
		Indented line 2
Multiple    spaces    between    words
Email: test@example.com
URL: https://example.com/path?query=value&param=123
Mathematical symbols: α β γ δ ε ∑ ∏ ∫ √ ∞
Emojis: 😀 🎉 🚀 💻 📚";

        // Act
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        chunks.Count.ShouldBeGreaterThan(0);

        // Verify character positions are valid
        foreach (var chunk in chunks)
        {
            chunk.StartIndex.ShouldBeGreaterThanOrEqualTo(0);
            chunk.StartIndex.ShouldBeLessThan(text.Length);
            chunk.EndIndex.ShouldBeGreaterThan(chunk.StartIndex);
            chunk.EndIndex.ShouldBeLessThanOrEqualTo(text.Length);

            // Verify extracted text matches substring
            var expectedText = text.Substring(chunk.StartIndex, chunk.EndIndex - chunk.StartIndex);
            chunk.Text.Trim().ShouldBe(expectedText.Trim());
        }
    }

    [Fact]
    public async Task ChunkAsync_Performance_ChunksLargeDocumentQuickly()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        // Generate ~50,000 character document
        var text = string.Join("\n", Enumerable.Range(1, 1000).Select(i =>
            $"Line {i}: The quick brown fox jumps over the lazy dog. Lorem ipsum dolor sit amet."));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());
        stopwatch.Stop();

        // Assert
        chunks.Count.ShouldBeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500,
            "Chunking ~50,000 chars should take less than 500ms");
    }

    [Fact]
    public async Task ChunkAsync_WithConfiguredOptions_RespectsSettings()
    {
        // Arrange - Create custom chunker with different options
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<ChunkingOptions>(options =>
        {
            options.ChunkSize = 256; // Smaller chunks
            options.OverlapSize = 64;
        });
        services.AddScoped<IChunkingStrategy, SlidingWindowChunker>();
        var sp = services.BuildServiceProvider();
        var customChunker = sp.GetRequiredService<IChunkingStrategy>();

        var documentId = Guid.NewGuid();
        var text = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"word{i}"));

        // Act
        var chunks = await customChunker.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        // With smaller chunk size, we should get more chunks
        chunks.Count.ShouldBeGreaterThan(2);
    }

    [Fact]
    public async Task ChunkAsync_VerifiesAllChunksHaveSequentialIndices()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));

        // Act
        var chunks = await _chunkingStrategy.ChunkAsync(text, documentId, Guid.NewGuid());

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].ChunkIndex.ShouldBe(i, $"Chunk at position {i} should have ChunkIndex {i}");
        }
    }
}
