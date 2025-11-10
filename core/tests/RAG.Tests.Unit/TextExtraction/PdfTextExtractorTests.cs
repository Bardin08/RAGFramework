using Microsoft.Extensions.Logging;
using Moq;
using RAG.Core.Exceptions;
using RAG.Infrastructure.TextExtraction;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.TextExtraction;

public class PdfTextExtractorTests
{
    private readonly PdfTextExtractor _extractor;
    private readonly Mock<ILogger<PdfTextExtractor>> _loggerMock;
    private readonly string _testDataPath;

    public PdfTextExtractorTests()
    {
        _loggerMock = new Mock<ILogger<PdfTextExtractor>>();
        _extractor = new PdfTextExtractor(_loggerMock.Object);
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidPdf_ReturnsTextContent()
    {
        // Arrange
        var pdfPath = Path.Combine(_testDataPath, "sample.pdf");
        if (!File.Exists(pdfPath))
        {
            // Skip test if sample PDF doesn't exist
            return;
        }

        using var stream = File.OpenRead(pdfPath);
        var fileName = "sample.pdf";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.ShouldNotBeNull();
        result.Text.ShouldNotBeNullOrEmpty();
        result.Text.ShouldContain("Sample PDF Document");
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidPdf_ExtractsMetadata()
    {
        // Arrange
        var pdfPath = Path.Combine(_testDataPath, "sample.pdf");
        if (!File.Exists(pdfPath))
        {
            return;
        }

        using var stream = File.OpenRead(pdfPath);
        var fileName = "sample.pdf";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Metadata.ShouldContainKey("OriginalFileName");
        result.Metadata["OriginalFileName"].ShouldBe(fileName);
        result.Metadata.ShouldContainKey("ExtractedAt");
        result.Metadata["ExtractedAt"].ShouldBeOfType<DateTime>();
    }

    [Fact]
    public async Task ExtractTextAsync_WithCorruptedPdf_ThrowsTextExtractionException()
    {
        // Arrange - Create corrupted PDF (truncated header)
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\nGarbage data"));
        var fileName = "corrupted.pdf";

        // Act & Assert
        await Should.ThrowAsync<TextExtractionException>(async () =>
            await _extractor.ExtractTextAsync(stream, fileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;
        var fileName = "test.pdf";

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
    public async Task ExtractTextAsync_WithInvalidPdfStructure_ThrowsTextExtractionException()
    {
        // Arrange - Create invalid PDF structure
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Not a PDF file at all"));
        var fileName = "invalid.pdf";

        // Act & Assert
        await Should.ThrowAsync<TextExtractionException>(async () =>
            await _extractor.ExtractTextAsync(stream, fileName));
    }
}
