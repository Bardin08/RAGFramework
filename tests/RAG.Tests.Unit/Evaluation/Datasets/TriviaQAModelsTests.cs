using RAG.Evaluation.Datasets;
using Xunit;

namespace RAG.Tests.Unit.Evaluation.Datasets;

public class TriviaQAModelsTests
{
    [Fact]
    public void TriviaQAAnswer_GetAllValidAnswers_ReturnsValueAndAliases()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "Paris",
            Aliases = new List<string> { "paris", "Paris, France", "Paree" }
        };

        // Act
        var allAnswers = answer.GetAllValidAnswers().ToList();

        // Assert
        Assert.Equal(4, allAnswers.Count);
        Assert.Contains("Paris", allAnswers);
        Assert.Contains("paris", allAnswers);
        Assert.Contains("Paris, France", allAnswers);
        Assert.Contains("Paree", allAnswers);
    }

    [Fact]
    public void TriviaQAAnswer_GetAllValidAnswers_WithNullAliases_ReturnsOnlyValue()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "Jupiter",
            Aliases = null
        };

        // Act
        var allAnswers = answer.GetAllValidAnswers().ToList();

        // Assert
        Assert.Single(allAnswers);
        Assert.Contains("Jupiter", allAnswers);
    }

    [Fact]
    public void TriviaQAAnswer_GetAllValidAnswers_WithEmptyAliases_ReturnsOnlyValue()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "Shakespeare",
            Aliases = new List<string>()
        };

        // Act
        var allAnswers = answer.GetAllValidAnswers().ToList();

        // Assert
        Assert.Single(allAnswers);
        Assert.Contains("Shakespeare", allAnswers);
    }

    [Fact]
    public void TriviaQAAnswer_GetAllValidAnswers_SkipsWhitespaceAliases()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "Answer",
            Aliases = new List<string> { "Valid Alias", "", "   ", null! }
        };

        // Act
        var allAnswers = answer.GetAllValidAnswers().ToList();

        // Assert
        Assert.Equal(2, allAnswers.Count);
        Assert.Contains("Answer", allAnswers);
        Assert.Contains("Valid Alias", allAnswers);
    }

    [Fact]
    public void TriviaQAAnswer_GetAllNormalizedAnswers_ReturnsLowercasedValues()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "PARIS",
            NormalizedAliases = new List<string> { "paris", "paris, france" }
        };

        // Act
        var normalizedAnswers = answer.GetAllNormalizedAnswers().ToList();

        // Assert
        Assert.Equal(3, normalizedAnswers.Count);
        Assert.Contains("paris", normalizedAnswers);
        Assert.Contains("paris, france", normalizedAnswers);
    }

    [Fact]
    public void TriviaQAAnswer_GetAllNormalizedAnswers_WithNullNormalizedAliases_ReturnsOnlyValue()
    {
        // Arrange
        var answer = new TriviaQAAnswer
        {
            Value = "Test Answer",
            NormalizedAliases = null
        };

        // Act
        var normalizedAnswers = answer.GetAllNormalizedAnswers().ToList();

        // Assert
        Assert.Single(normalizedAnswers);
        Assert.Contains("test answer", normalizedAnswers);
    }

    [Fact]
    public void TriviaQADocument_InitializesWithDefaults()
    {
        // Arrange & Act
        var document = new TriviaQADocument
        {
            DocumentId = "test-doc-1",
            QuestionId = "q1",
            Title = "Test Title",
            Content = "Test content",
            Source = "Wikipedia"
        };

        // Assert
        Assert.Equal("test-doc-1", document.DocumentId);
        Assert.Equal("q1", document.QuestionId);
        Assert.Equal("Test Title", document.Title);
        Assert.Equal("Test content", document.Content);
        Assert.Equal("Wikipedia", document.Source);
        Assert.Null(document.Url);
        Assert.NotNull(document.Metadata);
        Assert.Empty(document.Metadata);
    }

    [Fact]
    public void TriviaQADocument_SupportsMetadata()
    {
        // Arrange & Act
        var document = new TriviaQADocument
        {
            DocumentId = "test-doc-1",
            QuestionId = "q1",
            Title = "Test",
            Content = "Content",
            Source = "Web",
            Metadata = new Dictionary<string, object>
            {
                ["searchRank"] = 1,
                ["filename"] = "test.txt",
                ["custom"] = "value"
            }
        };

        // Assert
        Assert.Equal(3, document.Metadata.Count);
        Assert.Equal(1, document.Metadata["searchRank"]);
        Assert.Equal("test.txt", document.Metadata["filename"]);
        Assert.Equal("value", document.Metadata["custom"]);
    }

    [Fact]
    public void TriviaQAEntry_SupportsMultipleEvidenceTypes()
    {
        // Arrange & Act
        var entry = new TriviaQAEntry
        {
            QuestionId = "q1",
            Question = "What is the capital of France?",
            Answer = new TriviaQAAnswer { Value = "Paris" },
            EntityPages = new List<TriviaQAEntityPage>
            {
                new() { DocSource = "Paris", Title = "Paris", WikipediaText = "Paris content" }
            },
            SearchResults = new List<TriviaQASearchResult>
            {
                new() { DocSource = "paris-web", Title = "Paris Guide", PageText = "Web content" }
            }
        };

        // Assert
        Assert.NotNull(entry.EntityPages);
        Assert.Single(entry.EntityPages);
        Assert.NotNull(entry.SearchResults);
        Assert.Single(entry.SearchResults);
    }

    [Fact]
    public void TriviaQAEntityPage_StoresWikipediaMetadata()
    {
        // Arrange & Act
        var page = new TriviaQAEntityPage
        {
            DocSource = "Paris",
            Title = "Paris",
            Filename = "Paris.txt",
            WikipediaText = "Paris is the capital and most populous city of France..."
        };

        // Assert
        Assert.Equal("Paris", page.DocSource);
        Assert.Equal("Paris", page.Title);
        Assert.Equal("Paris.txt", page.Filename);
        Assert.Contains("Paris is the capital", page.WikipediaText);
    }

    [Fact]
    public void TriviaQASearchResult_StoresWebMetadata()
    {
        // Arrange & Act
        var result = new TriviaQASearchResult
        {
            DocSource = "paris-tourism",
            Title = "Visit Paris",
            Url = "https://example.com/paris",
            Filename = "paris-tourism.html",
            Description = "Guide to Paris tourism",
            Rank = 1,
            PageText = "Paris, the City of Light..."
        };

        // Assert
        Assert.Equal("paris-tourism", result.DocSource);
        Assert.Equal("Visit Paris", result.Title);
        Assert.Equal("https://example.com/paris", result.Url);
        Assert.Equal(1, result.Rank);
        Assert.Contains("Paris, the City of Light", result.PageText);
    }

    [Fact]
    public void TriviaQAEntry_SupportsQuestionSource()
    {
        // Arrange & Act
        var entry = new TriviaQAEntry
        {
            QuestionId = "q1",
            Question = "Test question?",
            QuestionSource = "Web",
            Answer = new TriviaQAAnswer { Value = "Answer" }
        };

        // Assert
        Assert.Equal("Web", entry.QuestionSource);
    }
}
