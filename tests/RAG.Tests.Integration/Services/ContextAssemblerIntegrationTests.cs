using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Application.Services;
using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Integration.Services;

/// <summary>
/// Integration tests for ContextAssembler.
/// Tests end-to-end context assembly with real dependencies.
/// </summary>
public class ContextAssemblerIntegrationTests
{
    private readonly ILogger<ContextAssembler> _logger;
    private readonly ITokenCounter _tokenCounter;
    private readonly ContextAssembler _assembler;

    public ContextAssemblerIntegrationTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ContextAssembler>();

        _tokenCounter = new ApproximateTokenCounter();

        var config = new ContextAssemblyConfig
        {
            MaxTokens = 3000,
            MinScore = 0.3,
            TokenCounterStrategy = "Approximate",
            EnableDeduplication = true
        };

        _assembler = new ContextAssembler(_tokenCounter, Options.Create(config), _logger);
    }

    [Fact]
    public void AssembleContext_WithRealRetrievalResults_CreatesValidContext()
    {
        // Arrange - Simulate realistic retrieval results from BM25/Dense
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.92,
                Text: "Retrieval-Augmented Generation (RAG) combines information retrieval with language generation to improve LLM accuracy by grounding responses in retrieved documents.",
                Source: "rag-overview.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.87,
                Text: "Dense Passage Retrieval (DPR) uses neural embeddings to find semantically similar documents, achieving higher recall than keyword-based methods like BM25.",
                Source: "dense-retrieval.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.75,
                Text: "BM25 is a probabilistic ranking function based on term frequency and inverse document frequency, widely used for keyword search in information retrieval.",
                Source: "bm25-algorithm.md"
            )
        };

        // Act
        var context = _assembler.AssembleContext(results);

        // Assert
        context.ShouldNotBeEmpty();

        // Verify source formatting
        context.ShouldContain("[Source 1: rag-overview.pdf]");
        context.ShouldContain("[Source 2: dense-retrieval.pdf]");
        context.ShouldContain("[Source 3: bm25-algorithm.md]");

        // Verify content inclusion
        context.ShouldContain("Retrieval-Augmented Generation");
        context.ShouldContain("Dense Passage Retrieval");
        context.ShouldContain("BM25 is a probabilistic");

        // Verify token limit is respected
        var actualTokens = _tokenCounter.CountTokens(context);
        actualTokens.ShouldBeLessThanOrEqualTo((int)(3000 * 0.9)); // Safe limit = 2700
    }

    [Fact]
    public void AssembleContext_WithManyShortChunks_HandlesCorrectly()
    {
        // Arrange - Edge case: Many short chunks
        var results = Enumerable.Range(1, 50).Select(i => new RetrievalResult(
            DocumentId: Guid.NewGuid(),
            Score: 1.0 - (i * 0.01), // Decreasing scores
            Text: $"Short chunk number {i} with minimal content.",
            Source: $"doc{i}.pdf"
        )).ToList();

        // Act - Use lower limit to force truncation
        var context = _assembler.AssembleContext(results, maxTokens: 400);

        // Assert
        context.ShouldNotBeEmpty();

        // Verify token limit
        var actualTokens = _tokenCounter.CountTokens(context);
        actualTokens.ShouldBeLessThanOrEqualTo((int)(400 * 0.9)); // Safe limit = 360

        // Verify some chunks included (but not all due to token limit)
        context.ShouldContain("[Source 1:");
        context.ShouldContain("Short chunk number 1");

        // Verify not all 50 chunks made it in
        context.ShouldNotContain("[Source 50:");
    }

    [Fact]
    public void AssembleContext_WithFewLongChunks_TruncatesCorrectly()
    {
        // Arrange - Edge case: Few very long chunks
        var longText = string.Join(" ", Enumerable.Repeat("This is a very long document with lots of repeated content.", 100));

        var results = new List<RetrievalResult>
        {
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.95,
                Text: longText,
                Source: "long-document-1.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.85,
                Text: longText,
                Source: "long-document-2.pdf"
            )
        };

        // Act
        var context = _assembler.AssembleContext(results, maxTokens: 1000);

        // Assert
        context.ShouldNotBeEmpty();

        // Verify token limit enforced
        var actualTokens = _tokenCounter.CountTokens(context);
        actualTokens.ShouldBeLessThanOrEqualTo((int)(1000 * 0.9)); // Safe limit = 900

        // Verify truncation occurred
        context.ShouldContain("[Source 1: long-document-1.pdf]");

        // May or may not include 2nd document depending on 1st chunk size
        // But total tokens should be within limit
    }

    [Fact]
    public void AssembleContext_WithDuplicateContent_RemovesDuplicates()
    {
        // Arrange - Realistic scenario: Same content from different sources
        var duplicateText = "Neural networks are computational models inspired by biological neural networks. They consist of layers of interconnected nodes.";

        var results = new List<RetrievalResult>
        {
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.9,
                Text: duplicateText,
                Source: "nn-basics-v1.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.85,
                Text: "Convolutional Neural Networks (CNNs) are specialized for processing grid-like data such as images.",
                Source: "cnn-overview.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.75,
                Text: duplicateText, // Exact duplicate
                Source: "nn-basics-v2.pdf"
            )
        };

        // Act
        var context = _assembler.AssembleContext(results);

        // Assert
        // Verify duplicate appears only once
        var occurrences = context.Split(duplicateText).Length - 1;
        occurrences.ShouldBe(1);

        // Verify other content is present
        context.ShouldContain("Convolutional Neural Networks");
        context.ShouldContain("computational models");

        // Verify only 2 sources (3rd is duplicate, should be removed)
        context.ShouldContain("[Source 1:");
        context.ShouldContain("[Source 2:");
        context.ShouldNotContain("[Source 3:");
    }

    [Fact]
    public void AssembleContext_WithMixedRelevanceScores_FiltersAppropriately()
    {
        // Arrange - Realistic relevance score distribution
        var results = new List<RetrievalResult>
        {
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.95,
                Text: "Highly relevant content about machine learning algorithms.",
                Source: "ml-algorithms.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.65,
                Text: "Moderately relevant content about data science.",
                Source: "data-science.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.35,
                Text: "Marginally relevant content about statistics.",
                Source: "statistics.pdf"
            ),
            new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.12,
                Text: "Irrelevant content about cooking recipes.",
                Source: "recipes.pdf"
            )
        };

        // Act
        var context = _assembler.AssembleContext(results);

        // Assert
        // High and moderate relevance should be included
        context.ShouldContain("machine learning algorithms");
        context.ShouldContain("data science");
        context.ShouldContain("statistics"); // Above 0.3 threshold

        // Low relevance should be filtered
        context.ShouldNotContain("cooking recipes");
    }

    [Fact]
    public void AssembleContext_WithCustomTokenLimit_RespectsLimit()
    {
        // Arrange
        var results = Enumerable.Range(1, 20).Select(i => new RetrievalResult(
            DocumentId: Guid.NewGuid(),
            Score: 1.0 - (i * 0.02),
            Text: "This is a test chunk with some content that should fit within token limits when assembled together with other chunks.",
            Source: $"test-doc-{i}.pdf"
        )).ToList();

        // Act - Custom token limit of 500
        var context = _assembler.AssembleContext(results, maxTokens: 500);

        // Assert
        var actualTokens = _tokenCounter.CountTokens(context);
        actualTokens.ShouldBeLessThanOrEqualTo((int)(500 * 0.9)); // Safe limit = 450

        context.ShouldContain("[Source 1:");
        context.ShouldNotBeEmpty();
    }
}
