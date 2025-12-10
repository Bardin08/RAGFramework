using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Evaluation.Datasets;
using Xunit;

namespace RAG.Tests.Unit.Evaluation.Datasets;

public class TriviaQAParserTests
{
    private readonly Mock<ILogger<TriviaQAParser>> _loggerMock;
    private readonly TriviaQAParser _parser;
    private readonly string _testDataDirectory;

    public TriviaQAParserTests()
    {
        _loggerMock = new Mock<ILogger<TriviaQAParser>>();
        _parser = new TriviaQAParser(_loggerMock.Object);
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "triviaqa-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDirectory);
    }

    [Fact]
    public async Task ParseAsync_ValidTriviaQAFile_ReturnsEntries()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "What is the capital of France?",
                    Answer = new
                    {
                        Value = "Paris",
                        Aliases = new[] { "paris", "Paris, France" },
                        NormalizedAliases = new[] { "paris", "paris, france" }
                    },
                    QuestionSource = "Web",
                    EntityPages = new[]
                    {
                        new
                        {
                            DocSource = "Paris",
                            Title = "Paris",
                            WikipediaText = "Paris is the capital of France..."
                        }
                    },
                    SearchResults = Array.Empty<object>()
                }
            }
        };

        var filePath = Path.Combine(_testDataDirectory, "test.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testData));

        // Act
        var entries = await _parser.ParseAsync(filePath);

        // Assert
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("q1", entries[0].QuestionId);
        Assert.Equal("What is the capital of France?", entries[0].Question);
        Assert.NotNull(entries[0].Answer);
        Assert.Equal("Paris", entries[0].Answer.Value);
        Assert.Equal(2, entries[0].Answer.Aliases?.Count);
    }

    [Fact]
    public async Task ParseAsync_ArrayFormat_ReturnsEntries()
    {
        // Arrange
        var testData = new[]
        {
            new
            {
                QuestionId = "q1",
                Question = "Who wrote Hamlet?",
                Answer = new
                {
                    Value = "William Shakespeare",
                    Aliases = new[] { "Shakespeare", "W. Shakespeare" }
                }
            }
        };

        var filePath = Path.Combine(_testDataDirectory, "array-test.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testData));

        // Act
        var entries = await _parser.ParseAsync(filePath);

        // Assert
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("q1", entries[0].QuestionId);
    }

    [Fact]
    public async Task ParseAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataDirectory, "nonexistent.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _parser.ParseAsync(filePath));
    }

    [Fact]
    public void ExtractDocuments_WithWikipediaEvidence_ExtractsWikipediaDocs()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Test question?",
                Answer = new TriviaQAAnswer { Value = "Test answer" },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new()
                    {
                        DocSource = "TestDoc",
                        Title = "Test Wikipedia Page",
                        WikipediaText = "This is test content from Wikipedia."
                    }
                }
            }
        };

        // Act
        var documents = _parser.ExtractDocuments(entries);

        // Assert
        Assert.NotNull(documents);
        Assert.Single(documents);
        Assert.Equal("Wikipedia", documents[0].Source);
        Assert.Equal("Test Wikipedia Page", documents[0].Title);
        Assert.Contains("Wikipedia", documents[0].Content);
    }

    [Fact]
    public void ExtractDocuments_WithWebEvidence_ExtractsWebDocs()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Test question?",
                Answer = new TriviaQAAnswer { Value = "Test answer" },
                SearchResults = new List<TriviaQASearchResult>
                {
                    new()
                    {
                        DocSource = "test-web-doc",
                        Title = "Test Web Page",
                        Url = "https://example.com/test",
                        PageText = "This is test content from the web.",
                        Rank = 1
                    }
                }
            }
        };

        // Act
        var documents = _parser.ExtractDocuments(entries);

        // Assert
        Assert.NotNull(documents);
        Assert.Single(documents);
        Assert.Equal("Web", documents[0].Source);
        Assert.Equal("Test Web Page", documents[0].Title);
        Assert.Equal("https://example.com/test", documents[0].Url);
        Assert.Contains("web", documents[0].Content);
    }

    [Fact]
    public void ExtractDocuments_DuplicateDocIds_ReturnsUniqueDocuments()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Test question 1?",
                Answer = new TriviaQAAnswer { Value = "Answer 1" },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "SameDoc", Title = "Same Doc", WikipediaText = "Content 1" }
                }
            },
            new()
            {
                QuestionId = "q1", // Same question ID and doc source
                Question = "Test question 2?",
                Answer = new TriviaQAAnswer { Value = "Answer 2" },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "SameDoc", Title = "Same Doc", WikipediaText = "Content 2" }
                }
            }
        };

        // Act
        var documents = _parser.ExtractDocuments(entries);

        // Assert
        Assert.NotNull(documents);
        Assert.Single(documents); // Should deduplicate by document ID
    }

    [Fact]
    public void ConvertToGroundTruth_ValidEntries_CreatesGroundTruth()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "What is the capital of France?",
                Answer = new TriviaQAAnswer
                {
                    Value = "Paris",
                    Aliases = new List<string> { "paris", "Paris, France" },
                    NormalizedAliases = new List<string> { "paris", "paris, france" }
                },
                QuestionSource = "Web",
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "Paris", WikipediaText = "Paris content" }
                }
            }
        };

        // Act
        var groundTruth = _parser.ConvertToGroundTruth(entries, "TestDataset");

        // Assert
        Assert.NotNull(groundTruth);
        Assert.Equal("TestDataset", groundTruth.Name);
        Assert.Single(groundTruth.Entries);

        var entry = groundTruth.Entries[0];
        Assert.Equal("What is the capital of France?", entry.Query);
        Assert.Equal("Paris", entry.ExpectedAnswer);
        Assert.NotNull(entry.AnswerAliases);
        // Note: "paris" is filtered out because it matches "Paris" case-insensitively
        Assert.Contains("Paris, France", entry.AnswerAliases);
        Assert.NotEmpty(entry.RelevantDocumentIds);
    }

    [Fact]
    public void ConvertToGroundTruth_GetAllValidAnswers_ReturnsAllAliases()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Test question?",
                Answer = new TriviaQAAnswer
                {
                    Value = "Primary Answer",
                    Aliases = new List<string> { "Alias 1", "Alias 2", "Alias 3" }
                },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "Doc1", WikipediaText = "Content" }
                }
            }
        };

        // Act
        var groundTruth = _parser.ConvertToGroundTruth(entries);
        var entry = groundTruth.Entries[0];
        var allAnswers = entry.GetAllValidAnswers().ToList();

        // Assert
        Assert.Contains("Primary Answer", allAnswers);
        Assert.Contains("Alias 1", allAnswers);
        Assert.Contains("Alias 2", allAnswers);
        Assert.Contains("Alias 3", allAnswers);
        Assert.Equal(4, allAnswers.Count);
    }

    [Fact]
    public void ConvertToGroundTruth_MissingQuestion_AddsValidationError()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "", // Empty question
                Answer = new TriviaQAAnswer { Value = "Answer" },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "Doc", WikipediaText = "Content" }
                }
            }
        };

        // Act
        var groundTruth = _parser.ConvertToGroundTruth(entries);

        // Assert
        Assert.NotNull(groundTruth.ValidationErrors);
        Assert.NotEmpty(groundTruth.ValidationErrors);
        Assert.Contains("Empty question", groundTruth.ValidationErrors[0]);
    }

    [Fact]
    public void ConvertToGroundTruth_MissingAnswer_AddsValidationError()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Valid question?",
                Answer = null, // Missing answer
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "Doc", WikipediaText = "Content" }
                }
            }
        };

        // Act
        var groundTruth = _parser.ConvertToGroundTruth(entries);

        // Assert
        Assert.NotNull(groundTruth.ValidationErrors);
        Assert.NotEmpty(groundTruth.ValidationErrors);
        Assert.Contains("Missing answer", groundTruth.ValidationErrors[0]);
    }

    [Fact]
    public void ConvertToGroundTruth_NoRelevantDocuments_AddsValidationError()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Valid question?",
                Answer = new TriviaQAAnswer { Value = "Valid answer" },
                EntityPages = null, // No evidence
                SearchResults = null
            }
        };

        // Act
        var groundTruth = _parser.ConvertToGroundTruth(entries);

        // Assert
        Assert.NotNull(groundTruth.ValidationErrors);
        Assert.NotEmpty(groundTruth.ValidationErrors);
        Assert.Contains("No relevant documents", groundTruth.ValidationErrors[0]);
    }

    [Fact]
    public async Task SaveDocumentsAsync_ValidDocuments_SavesFiles()
    {
        // Arrange
        var documents = new List<TriviaQADocument>
        {
            new()
            {
                DocumentId = "test-doc-1",
                QuestionId = "q1",
                Title = "Test Document",
                Content = "Test content",
                Source = "Wikipedia"
            }
        };

        var outputDir = Path.Combine(_testDataDirectory, "documents");

        // Act
        await _parser.SaveDocumentsAsync(documents, outputDir);

        // Assert
        var savedFile = Path.Combine(outputDir, "test-doc-1.json");
        Assert.True(File.Exists(savedFile));

        var json = await File.ReadAllTextAsync(savedFile);
        Assert.Contains("Test Document", json);
        Assert.Contains("Test content", json);
    }

    [Fact]
    public async Task SaveGroundTruthAsync_ValidGroundTruth_SavesFile()
    {
        // Arrange
        var entries = new List<TriviaQAEntry>
        {
            new()
            {
                QuestionId = "q1",
                Question = "Test question?",
                Answer = new TriviaQAAnswer { Value = "Test answer" },
                EntityPages = new List<TriviaQAEntityPage>
                {
                    new() { DocSource = "Doc", WikipediaText = "Content" }
                }
            }
        };

        var groundTruth = _parser.ConvertToGroundTruth(entries, "TestDataset");
        var outputPath = Path.Combine(_testDataDirectory, "ground-truth.json");

        // Act
        await _parser.SaveGroundTruthAsync(groundTruth, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));

        var json = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Test question?", json);
        Assert.Contains("Test answer", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
    }
}
