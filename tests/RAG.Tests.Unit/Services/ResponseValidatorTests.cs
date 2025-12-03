using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Services;
using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Services;

public class ResponseValidatorTests
{
    private readonly Mock<ILogger<ResponseValidator>> _mockLogger;
    private readonly ResponseValidator _validator;

    public ResponseValidatorTests()
    {
        _mockLogger = new Mock<ILogger<ResponseValidator>>();
        _validator = new ResponseValidator(_mockLogger.Object);
    }

    [Fact]
    public void ValidateResponse_WithValidResponseAndCitations_PassesValidation()
    {
        // Arrange
        var response = "The capital of France is Paris [Source 1]. It has a population of about 2 million [Source 2].";
        var query = "What is the capital of France and its population?";
        var retrievalResults = CreateSampleRetrievalResults(2);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
        result.CitationCount.ShouldBe(2);
        result.RelevanceScore.ShouldBeGreaterThan(0.3m);
    }

    [Fact]
    public void ValidateResponse_WithEmptyResponse_FailsValidation()
    {
        // Arrange
        var response = "";
        var query = "What is the capital of France?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Contains("empty"));
        result.CitationCount.ShouldBe(0);
        result.RelevanceScore.ShouldBe(0m);
    }

    [Fact]
    public void ValidateResponse_WithWhitespaceResponse_FailsValidation()
    {
        // Arrange
        var response = "   \n\t  ";
        var query = "What is the capital of France?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Contains("empty"));
    }

    [Fact]
    public void ValidateResponse_MissingCitations_FailsValidation()
    {
        // Arrange
        var response = "The capital of France is Paris. It has a population of about 2 million.";
        var query = "What is the capital of France and its population?";
        var retrievalResults = CreateSampleRetrievalResults(2);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Contains("source citations"));
        result.CitationCount.ShouldBe(0);
    }

    [Fact]
    public void ValidateResponse_IrrelevantResponse_FailsValidation()
    {
        // Arrange
        var response = "The weather is nice today. I like apples. [Source 1]";
        var query = "What is the capital of France?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Contains("relevant"));
        result.RelevanceScore.ShouldBeLessThan(0.3m);
    }

    [Fact]
    public void ValidateResponse_HighKeywordOverlap_PassesRelevanceCheck()
    {
        // Arrange
        var response = "Machine learning models use training data to learn patterns. " +
                      "Deep learning is a subset of machine learning [Source 1].";
        var query = "How do machine learning models learn from training data?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.RelevanceScore.ShouldBeGreaterThanOrEqualTo(0.3m);
        result.CitationCount.ShouldBe(1);
    }

    [Fact]
    public void ValidateResponse_LowKeywordOverlap_FailsRelevanceCheck()
    {
        // Arrange
        var response = "Cats are animals. Dogs bark. [Source 1]";
        var query = "How do neural networks process information?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.RelevanceScore.ShouldBeLessThan(0.3m);
        result.Issues.ShouldContain(i => i.Contains("relevant"));
    }

    [Fact]
    public void ValidateResponse_WithMultipleCitations_CountsCorrectly()
    {
        // Arrange
        var response = "Data from [Source 1] shows that [Source 2] confirms the theory. " +
                      "Additionally, [Source 3] provides evidence.";
        var query = "What evidence supports the theory?";
        var retrievalResults = CreateSampleRetrievalResults(3);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.CitationCount.ShouldBe(3);
    }

    [Fact]
    public void ValidateResponse_WithDuplicateCitations_CountsAll()
    {
        // Arrange
        var response = "According to [Source 1], the data shows trends. " +
                      "Furthermore, [Source 1] also mentions patterns.";
        var query = "What do the data trends show?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.CitationCount.ShouldBe(2); // Counts both instances
    }

    [Fact]
    public void ValidateResponse_CaseInsensitiveCitations_DetectsCorrectly()
    {
        // Arrange
        var response = "Data shows [source 1] and [SOURCE 2] confirm.";
        var query = "What does the data show?";
        var retrievalResults = CreateSampleRetrievalResults(2);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.CitationCount.ShouldBe(2);
    }

    [Fact]
    public void ValidateResponse_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var response = "Some response [Source 1]";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _validator.ValidateResponse(response, null!, retrievalResults));
    }

    [Fact]
    public void ValidateResponse_WithNullRetrievalResults_ThrowsArgumentNullException()
    {
        // Arrange
        var response = "Some response [Source 1]";
        var query = "What is this?";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _validator.ValidateResponse(response, query, null!));
    }

    [Fact]
    public void ValidateResponse_QueryWithOnlyStopWords_HandlesGracefully()
    {
        // Arrange
        var response = "This is a response with citations [Source 1].";
        var query = "what is the of and";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        // Should not crash, relevance score should be 0 (no meaningful keywords)
        result.RelevanceScore.ShouldBe(0m);
    }

    [Fact]
    public void ValidateResponse_ShortQuery_WorksCorrectly()
    {
        // Arrange
        var response = "Python is a programming language [Source 1].";
        var query = "Python?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.RelevanceScore.ShouldBeGreaterThan(0m);
        result.CitationCount.ShouldBe(1);
    }

    [Fact]
    public void ValidateResponse_ComplexQuery_CalculatesRelevanceCorrectly()
    {
        // Arrange
        var response = "Machine learning algorithms use training datasets to identify patterns " +
                      "and make predictions. Neural networks are particularly effective [Source 1].";
        var query = "How do machine learning algorithms use training data to make predictions?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.RelevanceScore.ShouldBeGreaterThan(0.3m); // High keyword overlap
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateResponse_PartialRelevance_LogsWarningButCalculatesScore()
    {
        // Arrange - response about Paris, query about economic policy (minimal overlap)
        var response = "Paris is the capital. The city has many museums and landmarks [Source 1].";
        var query = "What is the economic policy and taxation system?";
        var retrievalResults = CreateSampleRetrievalResults(1);

        // Act
        var result = _validator.ValidateResponse(response, query, retrievalResults);

        // Assert
        result.ShouldNotBeNull();
        result.RelevanceScore.ShouldBeLessThan(0.3m);
        result.Issues.ShouldContain(i => i.Contains("relevant"));
    }

    private List<RetrievalResult> CreateSampleRetrievalResults(int count)
    {
        var results = new List<RetrievalResult>();

        for (int i = 0; i < count; i++)
        {
            results.Add(new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.9 - (i * 0.1),
                Text: $"Sample text from document {i + 1}",
                Source: $"document{i + 1}.pdf"
            ));
        }

        return results;
    }
}
