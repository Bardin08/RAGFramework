using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Evaluation.Datasets;
using Xunit;

namespace RAG.Tests.Unit.Evaluation.Datasets;

public class TriviaQAProcessingServiceTests
{
    private readonly Mock<ILogger<TriviaQAParser>> _parserLoggerMock;
    private readonly Mock<ILogger<TriviaQAProcessingService>> _serviceLoggerMock;
    private readonly TriviaQAParser _parser;
    private readonly TriviaQAProcessingService _service;
    private readonly string _testDataDirectory;

    public TriviaQAProcessingServiceTests()
    {
        _parserLoggerMock = new Mock<ILogger<TriviaQAParser>>();
        _serviceLoggerMock = new Mock<ILogger<TriviaQAProcessingService>>();
        _parser = new TriviaQAParser(_parserLoggerMock.Object);
        _service = new TriviaQAProcessingService(_parser, _serviceLoggerMock.Object);
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "triviaqa-processing-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDirectory);
    }

    [Fact]
    public async Task ProcessDatasetAsync_ValidFile_ReturnsSuccessResult()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "What is 2+2?",
                    Answer = new
                    {
                        Value = "4",
                        Aliases = new[] { "four", "Four" }
                    },
                    EntityPages = new[]
                    {
                        new
                        {
                            DocSource = "Mathematics",
                            Title = "Basic Math",
                            WikipediaText = "Mathematics is the study of numbers..."
                        }
                    }
                },
                new
                {
                    QuestionId = "q2",
                    Question = "What is the largest planet?",
                    Answer = new { Value = "Jupiter", Aliases = new[] { "jupiter" } },
                    EntityPages = new[]
                    {
                        new
                        {
                            DocSource = "Jupiter",
                            Title = "Jupiter (planet)",
                            WikipediaText = "Jupiter is the largest planet..."
                        }
                    }
                }
            }
        };

        var rawFilePath = Path.Combine(_testDataDirectory, "test-dataset.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "output");

        // Act
        var result = await _service.ProcessDatasetAsync(
            rawFilePath,
            outputDir,
            "TestDataset");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalQuestions);
        Assert.Equal(2, result.TotalDocuments);
        Assert.Equal(2, result.ValidGroundTruthEntries);
        Assert.Equal(0, result.ValidationErrors);
        Assert.NotNull(result.DocumentsOutputPath);
        Assert.NotNull(result.GroundTruthOutputPath);
        Assert.True(File.Exists(result.GroundTruthOutputPath));
    }

    [Fact]
    public async Task ProcessDatasetAsync_EmptyFile_ReturnsFailure()
    {
        // Arrange
        var testData = new { Data = Array.Empty<object>() };
        var rawFilePath = Path.Combine(_testDataDirectory, "empty-dataset.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "output-empty");

        // Act
        var result = await _service.ProcessDatasetAsync(rawFilePath, outputDir);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.TotalQuestions);
        Assert.Contains("No entries found", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessDatasetAsync_CreatesOutputDirectories()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "Test?",
                    Answer = new { Value = "Answer" },
                    EntityPages = new[]
                    {
                        new { DocSource = "Doc", WikipediaText = "Content" }
                    }
                }
            }
        };

        var rawFilePath = Path.Combine(_testDataDirectory, "test.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "new-output");

        // Act
        var result = await _service.ProcessDatasetAsync(rawFilePath, outputDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(outputDir, "processed")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "processed", "documents")));
    }

    [Fact]
    public async Task ProcessDatasetAsync_SetsTimestamps()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "Test?",
                    Answer = new { Value = "Answer" },
                    EntityPages = new[]
                    {
                        new { DocSource = "Doc", WikipediaText = "Content" }
                    }
                }
            }
        };

        var rawFilePath = Path.Combine(_testDataDirectory, "timestamp-test.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "timestamp-output");

        // Act
        var result = await _service.ProcessDatasetAsync(rawFilePath, outputDir);

        // Assert
        Assert.NotEqual(default, result.StartedAt);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.CompletedAt > result.StartedAt);
        Assert.NotNull(result.Duration);
        Assert.True(result.Duration.Value.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ProcessDatasetAsync_WithValidationErrors_RecordsErrors()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "Valid question?",
                    Answer = new { Value = "Valid answer" },
                    EntityPages = new[]
                    {
                        new { DocSource = "Doc", WikipediaText = "Content" }
                    }
                },
                new
                {
                    QuestionId = "q2",
                    Question = "", // Invalid: empty question
                    Answer = new { Value = "Answer" },
                    EntityPages = new[]
                    {
                        new { DocSource = "Doc2", WikipediaText = "Content" }
                    }
                }
            }
        };

        var rawFilePath = Path.Combine(_testDataDirectory, "validation-test.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "validation-output");

        // Act
        var result = await _service.ProcessDatasetAsync(rawFilePath, outputDir);

        // Assert
        Assert.True(result.Success); // Still succeeds but records errors
        Assert.Equal(2, result.TotalQuestions);
        Assert.Equal(1, result.ValidGroundTruthEntries); // Only 1 valid
        Assert.Equal(1, result.ValidationErrors); // 1 error
    }

    [Fact]
    public async Task LoadProcessedDocumentsAsync_ExistingDocuments_LoadsSuccessfully()
    {
        // Arrange
        var documentsDir = Path.Combine(_testDataDirectory, "load-test", "documents");
        Directory.CreateDirectory(documentsDir);

        var doc1 = new TriviaQADocument
        {
            DocumentId = "doc-1",
            QuestionId = "q1",
            Title = "Document 1",
            Content = "Content 1",
            Source = "Wikipedia"
        };

        var doc2 = new TriviaQADocument
        {
            DocumentId = "doc-2",
            QuestionId = "q2",
            Title = "Document 2",
            Content = "Content 2",
            Source = "Web"
        };

        await File.WriteAllTextAsync(
            Path.Combine(documentsDir, "doc-1.json"),
            JsonSerializer.Serialize(doc1));

        await File.WriteAllTextAsync(
            Path.Combine(documentsDir, "doc-2.json"),
            JsonSerializer.Serialize(doc2));

        // Act
        var documents = await _service.LoadProcessedDocumentsAsync(documentsDir);

        // Assert
        Assert.NotNull(documents);
        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, d => d.DocumentId == "doc-1");
        Assert.Contains(documents, d => d.DocumentId == "doc-2");
    }

    [Fact]
    public async Task LoadProcessedDocumentsAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDataDirectory, "nonexistent");

        // Act
        var documents = await _service.LoadProcessedDocumentsAsync(nonExistentDir);

        // Assert
        Assert.NotNull(documents);
        Assert.Empty(documents);
    }

    [Fact]
    public async Task LoadProcessedDocumentsAsync_CorruptedFile_SkipsFile()
    {
        // Arrange
        var documentsDir = Path.Combine(_testDataDirectory, "corrupted-test", "documents");
        Directory.CreateDirectory(documentsDir);

        // Valid document
        var validDoc = new TriviaQADocument
        {
            DocumentId = "valid-doc",
            QuestionId = "q1",
            Title = "Valid Document",
            Content = "Valid content",
            Source = "Wikipedia"
        };

        await File.WriteAllTextAsync(
            Path.Combine(documentsDir, "valid.json"),
            JsonSerializer.Serialize(validDoc));

        // Corrupted document
        await File.WriteAllTextAsync(
            Path.Combine(documentsDir, "corrupted.json"),
            "{ invalid json content");

        // Act
        var documents = await _service.LoadProcessedDocumentsAsync(documentsDir);

        // Assert
        Assert.NotNull(documents);
        Assert.Single(documents); // Only valid document loaded
        Assert.Equal("valid-doc", documents[0].DocumentId);
    }

    [Fact]
    public async Task ProcessDatasetAsync_SavesDocumentsWithMetadata()
    {
        // Arrange
        var testData = new
        {
            Data = new[]
            {
                new
                {
                    QuestionId = "q1",
                    Question = "Test question?",
                    Answer = new { Value = "Test answer" },
                    SearchResults = new[]
                    {
                        new
                        {
                            DocSource = "web-doc",
                            Title = "Web Document",
                            Url = "https://example.com",
                            PageText = "Web content",
                            Rank = 1,
                            Description = "Test description"
                        }
                    }
                }
            }
        };

        var rawFilePath = Path.Combine(_testDataDirectory, "metadata-test.json");
        await File.WriteAllTextAsync(rawFilePath, JsonSerializer.Serialize(testData));

        var outputDir = Path.Combine(_testDataDirectory, "metadata-output");

        // Act
        var result = await _service.ProcessDatasetAsync(rawFilePath, outputDir);

        // Assert
        Assert.True(result.Success);

        var documents = await _service.LoadProcessedDocumentsAsync(
            Path.Combine(outputDir, "processed", "documents"));

        Assert.Single(documents);
        var doc = documents[0];
        Assert.Equal("Web", doc.Source);
        Assert.Equal("https://example.com", doc.Url);
        Assert.Contains("searchRank", doc.Metadata.Keys);
        // When deserialized from JSON, metadata values are JsonElement, so convert for comparison
        var searchRank = doc.Metadata["searchRank"];
        if (searchRank is JsonElement jsonElement)
        {
            Assert.Equal(1, jsonElement.GetInt32());
        }
        else
        {
            Assert.Equal(1, searchRank);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
    }
}
