using RAG.Application.DTOs;
using RAG.Application.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

public class QueryProcessorTests
{
    private readonly QueryProcessor _processor;

    public QueryProcessorTests()
    {
        _processor = new QueryProcessor();
    }

    [Fact]
    public void ProcessQuery_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullQuery = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _processor.ProcessQuery(nullQuery!));
    }

    [Fact]
    public void ProcessQuery_WithEmptyString_ReturnsValidProcessedQuery()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var result = _processor.ProcessQuery(emptyQuery);

        // Assert
        result.ShouldNotBeNull();
        result.OriginalText.ShouldBe("");
        result.NormalizedText.ShouldBe("");
        result.Language.ShouldBe("en");
        result.Tokens.ShouldBeEmpty();
    }

    [Fact]
    public void ProcessQuery_WithWhitespaceOnly_ReturnsEmptyNormalizedText()
    {
        // Arrange
        var whitespaceQuery = "   ";

        // Act
        var result = _processor.ProcessQuery(whitespaceQuery);

        // Assert
        result.NormalizedText.ShouldBe("");
        result.Tokens.ShouldBeEmpty();
    }

    [Fact]
    public void ProcessQuery_WithEnglishQuery_DetectsEnglishLanguage()
    {
        // Arrange
        var englishQuery = "What is RAG?";

        // Act
        var result = _processor.ProcessQuery(englishQuery);

        // Assert
        result.Language.ShouldBe("en");
        result.NormalizedText.ShouldBe("what is rag?");
        result.OriginalText.ShouldBe("What is RAG?");
    }

    [Fact]
    public void ProcessQuery_WithUkrainianQuery_DetectsUkrainianLanguage()
    {
        // Arrange
        var ukrainianQuery = "Що таке RAG?";

        // Act
        var result = _processor.ProcessQuery(ukrainianQuery);

        // Assert
        result.Language.ShouldBe("uk");
        result.NormalizedText.ShouldBe("що таке rag?");
        result.OriginalText.ShouldBe("Що таке RAG?");
    }

    [Fact]
    public void ProcessQuery_WithMixedCase_ConvertsToLowercase()
    {
        // Arrange
        var mixedCaseQuery = "ReTrIeVaL";

        // Act
        var result = _processor.ProcessQuery(mixedCaseQuery);

        // Assert
        result.NormalizedText.ShouldBe("retrieval");
    }

    [Fact]
    public void ProcessQuery_WithMultipleSpaces_RemovesExtraSpaces()
    {
        // Arrange
        var multiSpaceQuery = "hello    world";

        // Act
        var result = _processor.ProcessQuery(multiSpaceQuery);

        // Assert
        result.NormalizedText.ShouldBe("hello world");
    }

    [Fact]
    public void ProcessQuery_WithLeadingAndTrailingSpaces_TrimsCorrectly()
    {
        // Arrange
        var spacedQuery = "  Test Query  ";

        // Act
        var result = _processor.ProcessQuery(spacedQuery);

        // Assert
        result.NormalizedText.ShouldBe("test query");
        result.Tokens.ShouldBe(new List<string> { "test", "query" });
    }

    [Fact]
    public void ProcessQuery_TokenizesCorrectly()
    {
        // Arrange
        var query = "hello world test";

        // Act
        var result = _processor.ProcessQuery(query);

        // Assert
        result.Tokens.ShouldBe(new List<string> { "hello", "world", "test" });
    }

    [Fact]
    public void ProcessQuery_WithSpecialCharacters_PreservesThemInNormalization()
    {
        // Arrange
        var queryWithSpecialChars = "What is AI/ML?";

        // Act
        var result = _processor.ProcessQuery(queryWithSpecialChars);

        // Assert
        result.NormalizedText.ShouldBe("what is ai/ml?");
        result.Tokens.ShouldBe(new List<string> { "what", "is", "ai/ml?" });
    }

    [Fact]
    public void ProcessQuery_WithMixedLanguageQuery_DetectsUkrainian()
    {
        // Arrange
        var mixedQuery = "Hello Привіт";

        // Act
        var result = _processor.ProcessQuery(mixedQuery);

        // Assert
        result.Language.ShouldBe("uk"); // Cyrillic detected
    }

    [Theory]
    [MemberData(nameof(GetLanguageDetectionTestData))]
    public void ProcessQuery_LanguageDetectionAccuracy_MeetsThreshold(string query, string expectedLanguage)
    {
        // Act
        var result = _processor.ProcessQuery(query);

        // Assert
        result.Language.ShouldBe(expectedLanguage);
    }

    public static IEnumerable<object[]> GetLanguageDetectionTestData()
    {
        // 10 English queries
        yield return new object[] { "What is retrieval augmented generation?", "en" };
        yield return new object[] { "How does machine learning work?", "en" };
        yield return new object[] { "Document processing pipeline", "en" };
        yield return new object[] { "Search engine optimization", "en" };
        yield return new object[] { "Natural language processing", "en" };
        yield return new object[] { "Information retrieval system", "en" };
        yield return new object[] { "Vector database query", "en" };
        yield return new object[] { "Semantic search algorithm", "en" };
        yield return new object[] { "Text embedding model", "en" };
        yield return new object[] { "BM25 ranking function", "en" };

        // 10 Ukrainian queries
        yield return new object[] { "Що таке RAG?", "uk" };
        yield return new object[] { "Як працює пошук?", "uk" };
        yield return new object[] { "Обробка документів", "uk" };
        yield return new object[] { "Система індексації", "uk" };
        yield return new object[] { "Векторна база даних", "uk" };
        yield return new object[] { "Семантичний пошук", "uk" };
        yield return new object[] { "Аналіз текстів", "uk" };
        yield return new object[] { "Машинне навчання", "uk" };
        yield return new object[] { "Алгоритми ранжування", "uk" };
        yield return new object[] { "Модель embeddings", "uk" };
    }

    [Fact]
    public void ProcessQuery_LanguageDetectionAccuracy_IsAtLeast95Percent()
    {
        // Arrange
        var testData = GetLanguageDetectionTestData().ToList();
        var correctDetections = 0;

        // Act
        foreach (var data in testData)
        {
            var query = (string)data[0];
            var expectedLanguage = (string)data[1];
            var result = _processor.ProcessQuery(query);

            if (result.Language == expectedLanguage)
                correctDetections++;
        }

        // Assert
        var accuracy = (double)correctDetections / testData.Count;
        accuracy.ShouldBeGreaterThanOrEqualTo(0.95); // ≥ 95% accuracy required (AC#8)
    }

    [Fact]
    public void ProcessQuery_Performance_CompletesUnder5Milliseconds()
    {
        // Arrange
        var query = "This is a test query for performance measurement";
        var iterations = 100;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _processor.ProcessQuery(query);
        }
        stopwatch.Stop();

        // Assert
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        avgTimeMs.ShouldBeLessThan(5.0); // < 5ms per query (NFR requirement)
    }

    [Fact]
    public void ProcessQuery_PreservesOriginalText()
    {
        // Arrange
        var originalQuery = "  HELLO  World  ";

        // Act
        var result = _processor.ProcessQuery(originalQuery);

        // Assert
        result.OriginalText.ShouldBe(originalQuery);
        result.NormalizedText.ShouldBe("hello world");
    }
}
