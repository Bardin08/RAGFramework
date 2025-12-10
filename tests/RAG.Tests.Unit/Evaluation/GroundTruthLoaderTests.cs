using Microsoft.Extensions.Logging.Abstractions;
using RAG.Evaluation.GroundTruth;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class GroundTruthLoaderTests
{
    private readonly JsonGroundTruthLoader _jsonLoader;
    private readonly CsvGroundTruthLoader _csvLoader;

    public GroundTruthLoaderTests()
    {
        _jsonLoader = new JsonGroundTruthLoader(NullLogger<JsonGroundTruthLoader>.Instance);
        _csvLoader = new CsvGroundTruthLoader(NullLogger<CsvGroundTruthLoader>.Instance);
    }

    [Fact]
    public void JsonLoader_CanHandle_JsonFile()
    {
        _jsonLoader.CanHandle("test.json").ShouldBeTrue();
        _jsonLoader.CanHandle("test.JSON").ShouldBeTrue();
        _jsonLoader.CanHandle("test.csv").ShouldBeFalse();
    }

    [Fact]
    public void CsvLoader_CanHandle_CsvFile()
    {
        _csvLoader.CanHandle("test.csv").ShouldBeTrue();
        _csvLoader.CanHandle("test.CSV").ShouldBeTrue();
        _csvLoader.CanHandle("test.json").ShouldBeFalse();
    }

    [Fact]
    public async Task JsonLoader_LoadAsync_NonExistentFile_ReturnsError()
    {
        var result = await _jsonLoader.LoadAsync("/nonexistent/file.json");

        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.ShouldContain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task CsvLoader_LoadAsync_NonExistentFile_ReturnsError()
    {
        var result = await _csvLoader.LoadAsync("/nonexistent/file.csv");

        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.ShouldContain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task JsonLoader_LoadAsync_ValidFile_ParsesEntries()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(tempFile, """
            [
                {
                    "query": "What is the capital of France?",
                    "expectedAnswer": "Paris",
                    "relevantDocuments": ["doc-001", "doc-002"]
                }
            ]
            """);

        try
        {
            // Act
            var result = await _jsonLoader.LoadAsync(tempFile);

            // Assert
            result.IsValid.ShouldBeTrue();
            result.Entries.Count.ShouldBe(1);
            result.Entries[0].Query.ShouldBe("What is the capital of France?");
            result.Entries[0].ExpectedAnswer.ShouldBe("Paris");
            result.Entries[0].RelevantDocumentIds.ShouldBe(new[] { "doc-001", "doc-002" });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CsvLoader_LoadAsync_ValidFile_ParsesEntries()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".csv";
        await File.WriteAllTextAsync(tempFile, """
            query,expected_answer,relevant_docs
            "What is the capital of France?","Paris","doc-001;doc-002"
            """);

        try
        {
            // Act
            var result = await _csvLoader.LoadAsync(tempFile);

            // Assert
            result.IsValid.ShouldBeTrue();
            result.Entries.Count.ShouldBe(1);
            result.Entries[0].Query.ShouldBe("What is the capital of France?");
            result.Entries[0].ExpectedAnswer.ShouldBe("Paris");
            result.Entries[0].RelevantDocumentIds.ShouldBe(new[] { "doc-001", "doc-002" });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonLoader_LoadAsync_MissingQuery_ReturnsValidationError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(tempFile, """
            [
                {
                    "expectedAnswer": "Paris",
                    "relevantDocuments": ["doc-001"]
                }
            ]
            """);

        try
        {
            // Act
            var result = await _jsonLoader.LoadAsync(tempFile);

            // Assert
            result.ValidationErrors.ShouldContain(e => e.Contains("Query is required"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CsvLoader_LoadAsync_HandlesQuotedCommas()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".csv";
        await File.WriteAllTextAsync(tempFile, """
            query,expected_answer,relevant_docs
            "What is 1+1, and why?","2, because math","doc-001;doc-002"
            """);

        try
        {
            // Act
            var result = await _csvLoader.LoadAsync(tempFile);

            // Assert
            result.IsValid.ShouldBeTrue();
            result.Entries[0].Query.ShouldBe("What is 1+1, and why?");
            result.Entries[0].ExpectedAnswer.ShouldBe("2, because math");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
