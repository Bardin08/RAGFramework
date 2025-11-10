using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Core.Exceptions;
using RAG.Infrastructure.TextExtraction;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.TextExtraction;

public class DocxTextExtractorTests
{
    private readonly DocxTextExtractor _extractor;
    private readonly Mock<ILogger<DocxTextExtractor>> _loggerMock;

    public DocxTextExtractorTests()
    {
        _loggerMock = new Mock<ILogger<DocxTextExtractor>>();
        _extractor = new DocxTextExtractor(_loggerMock.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidDocx_ReturnsTextContent()
    {
        // Arrange
        var testContent = new[] { "First paragraph", "Second paragraph", "Third paragraph" };
        using var stream = CreateTestDocx(testContent);
        var fileName = "test.docx";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.ShouldNotBeNull();
        result.Text.ShouldNotBeNullOrEmpty();
        foreach (var line in testContent)
        {
            result.Text.ShouldContain(line);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidDocx_ExtractsMetadata()
    {
        // Arrange
        var testContent = new[] { "Sample content" };
        var title = "Test Document";
        var author = "Test Author";
        using var stream = CreateTestDocx(testContent, title, author);
        var fileName = "sample.docx";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Metadata.ShouldContainKey("OriginalFileName");
        result.Metadata["OriginalFileName"].ShouldBe(fileName);
        result.Metadata.ShouldContainKey("ExtractedAt");
        result.Metadata["ExtractedAt"].ShouldBeOfType<DateTime>();

        if (result.Metadata.ContainsKey("Title"))
            result.Metadata["Title"].ShouldBe(title);

        if (result.Metadata.ContainsKey("Author"))
            result.Metadata["Author"].ShouldBe(author);
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyDocx_ReturnsEmptyString()
    {
        // Arrange
        using var stream = CreateTestDocx(Array.Empty<string>());
        var fileName = "empty.docx";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Text.ShouldBeEmpty();
        result.Metadata.ShouldContainKey("OriginalFileName");
    }

    [Fact]
    public async Task ExtractTextAsync_WithCorruptedDocx_ThrowsTextExtractionException()
    {
        // Arrange - Create corrupted DOCX (invalid ZIP structure)
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Not a valid DOCX file"));
        var fileName = "corrupted.docx";

        // Act & Assert
        await Should.ThrowAsync<TextExtractionException>(async () =>
            await _extractor.ExtractTextAsync(stream, fileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;
        var fileName = "test.docx";

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _extractor.ExtractTextAsync(nullStream!, fileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullFileName_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream();
        string? nullFileName = null;

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _extractor.ExtractTextAsync(stream, nullFileName!));
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyFileName_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var emptyFileName = string.Empty;

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _extractor.ExtractTextAsync(stream, emptyFileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithMultipleParagraphs_ExtractsAllText()
    {
        // Arrange
        var paragraphs = new[]
        {
            "Paragraph 1: Introduction",
            "Paragraph 2: Main content with special chars @#$%",
            "Paragraph 3: Unicode content Українська 中文",
            "Paragraph 4: Conclusion"
        };
        using var stream = CreateTestDocx(paragraphs);
        var fileName = "multi-para.docx";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        foreach (var paragraph in paragraphs)
        {
            result.Text.ShouldContain(paragraph);
        }
    }

    /// <summary>
    /// Helper method to create a test DOCX file in memory.
    /// </summary>
    private MemoryStream CreateTestDocx(string[] paragraphs, string? title = null, string? author = null)
    {
        var stream = new MemoryStream();

        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            // Set package properties
            wordDocument.PackageProperties.Title = title;
            wordDocument.PackageProperties.Creator = author;
            wordDocument.PackageProperties.Created = DateTime.UtcNow;
            wordDocument.PackageProperties.Modified = DateTime.UtcNow;

            // Create main document part
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            // Add paragraphs
            foreach (var paragraphText in paragraphs)
            {
                var paragraph = new Paragraph();
                var run = new Run();
                var text = new Text(paragraphText);
                run.Append(text);
                paragraph.Append(run);
                body.Append(paragraph);
            }

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
