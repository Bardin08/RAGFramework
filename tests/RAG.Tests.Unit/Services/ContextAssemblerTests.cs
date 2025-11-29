using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Configuration;
using RAG.Application.Services;
using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for ContextAssembler service.
/// Tests cover AC#2 (token limiting, truncation), AC#4 (source formatting),
/// AC#5 (deduplication), AC#6 (relevance filtering), AC#7 (validation).
/// </summary>
public class ContextAssemblerTests
{
    private readonly Mock<ITokenCounter> _mockTokenCounter;
    private readonly Mock<ILogger<ContextAssembler>> _mockLogger;
    private readonly ContextAssemblyConfig _config;
    private readonly ContextAssembler _assembler;

    public ContextAssemblerTests()
    {
        _mockTokenCounter = new Mock<ITokenCounter>();
        _mockLogger = new Mock<ILogger<ContextAssembler>>();
        _config = new ContextAssemblyConfig
        {
            MaxTokens = 3000,
            MinScore = 0.3,
            TokenCounterStrategy = "Approximate",
            EnableDeduplication = true
        };

        var options = Options.Create(_config);
        _assembler = new ContextAssembler(_mockTokenCounter.Object, options, _mockLogger.Object);
    }

    [Fact]
    public void AssembleContext_WithEmptyResults_ReturnsEmptyString()
    {
        // Arrange
        var emptyResults = new List<RetrievalResult>();

        // Act
        var context = _assembler.AssembleContext(emptyResults);

        // Assert
        context.ShouldBeEmpty();
    }

    [Fact]
    public void AssembleContext_WithNullResults_ReturnsEmptyString()
    {
        // Arrange
        List<RetrievalResult>? nullResults = null;

        // Act
        var context = _assembler.AssembleContext(nullResults!);

        // Assert
        context.ShouldBeEmpty();
    }

    [Fact]
    public void AssembleContext_FormatsWithSourceReferences()
    {
        // Arrange - AC#4: Test source formatting
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.9,
                Text: "Machine learning is a subset of AI",
                Source: "ml-intro.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.8,
                Text: "Neural networks are computational models",
                Source: "nn-basics.docx"
            )
        };

        // Mock token counter for predictable behavior
        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - AC#4: Verify [Source X: file.pdf] format
        context.ShouldContain("[Source 1: ml-intro.pdf]");
        context.ShouldContain("[Source 2: nn-basics.docx]");
        context.ShouldContain("Machine learning is a subset of AI");
        context.ShouldContain("Neural networks are computational models");
    }

    [Fact]
    public void AssembleContext_FiltersBelowMinScore()
    {
        // Arrange - AC#6: Test relevance filtering
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, "High relevance", "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.2, "Low relevance", "doc2.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.5, "Medium relevance", "doc3.pdf")
        };

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - AC#6: Low score result should be filtered out
        context.ShouldContain("High relevance");
        context.ShouldContain("Medium relevance");
        context.ShouldNotContain("Low relevance");
    }

    [Fact]
    public void AssembleContext_KeepsAtLeastOneResult_EvenIfBelowThreshold()
    {
        // Arrange - AC#6: Test fallback behavior when all results below threshold
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.15, "Best of bad", "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.05, "Worst", "doc2.pdf")
        };

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - Should include highest scored result even though below minScore
        context.ShouldContain("Best of bad");
        context.ShouldNotContain("Worst");
    }

    [Fact]
    public void AssembleContext_SortsResultsByScoreDescending()
    {
        // Arrange - AC#2: Test sorting by score
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.5, "Third best", "doc3.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.9, "Best", "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.7, "Second best", "doc2.pdf")
        };

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - Verify ordering by checking Source numbers
        var bestIndex = context.IndexOf("[Source 1:");
        var secondIndex = context.IndexOf("[Source 2:");
        var thirdIndex = context.IndexOf("[Source 3:");

        bestIndex.ShouldBeLessThan(secondIndex);
        secondIndex.ShouldBeLessThan(thirdIndex);
        context.ShouldContain("Best");
        context.ShouldContain("Second best");
        context.ShouldContain("Third best");
    }

    [Fact]
    public void AssembleContext_RespectsTokenLimit()
    {
        // Arrange - AC#2, AC#7: Test token limit enforcement
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, "First chunk", "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.8, "Second chunk", "doc2.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.7, "Third chunk", "doc3.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.6, "Fourth chunk", "doc4.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.5, "Fifth chunk", "doc5.pdf")
        };

        // Mock: Each chunk = 400 tokens, limit = 1000 tokens (safe limit = 900)
        // Should fit 2 chunks (800 tokens), truncate 3rd
        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(400);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 1000);

        // Assert - Should include first 2 chunks, may truncate 3rd
        context.ShouldContain("[Source 1: doc1.pdf]");
        context.ShouldContain("[Source 2: doc2.pdf]");
        // 3rd chunk may or may not appear (depends on truncation logic)
        context.ShouldNotContain("[Source 5: doc5.pdf]"); // Definitely not 5th
    }

    [Fact]
    public void AssembleContext_TruncatesLastDocument_WhenExceedingLimit()
    {
        // Arrange - AC#2, AC#7: Test truncation behavior
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, "First chunk", "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.8, "Second chunk will be truncated", "doc2.pdf")
        };

        var callCount = 0;
        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>()))
            .Returns(() =>
            {
                callCount++;
                return callCount <= 1 ? 850 : 400; // First chunk 850, second chunk 400 (total would be 1250, exceeds 900 safe limit)
            });

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 1000);

        // Assert - Should truncate 2nd chunk
        context.ShouldContain("[Source 1: doc1.pdf]");
        context.ShouldContain("[Source 2: doc2.pdf]");
        context.ShouldContain("..."); // Truncation indicator
    }

    [Fact]
    public void AssembleContext_RemovesDuplicateChunks()
    {
        // Arrange - AC#5, AC#7: Test deduplication
        var duplicateText = "This is duplicate content";
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, duplicateText, "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.8, "Unique content", "doc2.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.7, duplicateText, "doc3.pdf") // Duplicate
        };

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - AC#5: Duplicate should appear only once (keeping higher scored one)
        var count = context.Split(duplicateText).Length - 1;
        count.ShouldBe(1); // Should appear exactly once
        context.ShouldContain("Unique content");
    }

    [Fact]
    public void AssembleContext_AppliesSafetyBuffer()
    {
        // Arrange - AC#2, AC#7: Test 10% safety buffer
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, "Test chunk", "doc.pdf")
        };

        var maxTokens = 1000;
        var safeLimit = (int)(maxTokens * 0.9); // 900

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(950); // Exceeds safe limit

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: maxTokens);

        // Assert - Should truncate because exceeds 900 safe limit
        context.ShouldContain("...");
    }

    [Fact]
    public void AssembleContext_WithDeduplicationDisabled_KeepsDuplicates()
    {
        // Arrange
        var duplicateText = "Duplicate content";
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.9, duplicateText, "doc1.pdf"),
            new RetrievalResult(Guid.NewGuid(), 0.8, duplicateText, "doc2.pdf")
        };

        var configWithoutDedup = new ContextAssemblyConfig
        {
            MaxTokens = 3000,
            MinScore = 0.3,
            EnableDeduplication = false
        };

        var assembler = new ContextAssembler(
            _mockTokenCounter.Object,
            Options.Create(configWithoutDedup),
            _mockLogger.Object);

        _mockTokenCounter.Setup(tc => tc.CountTokens(It.IsAny<string>())).Returns(100);

        // Act
        var context = assembler.AssembleContext(results, maxTokens: 5000);

        // Assert - Should have both duplicates
        var count = context.Split(duplicateText).Length - 1;
        count.ShouldBe(2); // Should appear twice
    }
}
