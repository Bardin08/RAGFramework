using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Core.Exceptions;
using RAG.Infrastructure.TextExtraction;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.TextExtraction;

public class TxtTextExtractorTests
{
    private readonly TxtTextExtractor _extractor;
    private readonly Mock<ILogger<TxtTextExtractor>> _loggerMock;

    public TxtTextExtractorTests()
    {
        _loggerMock = new Mock<ILogger<TxtTextExtractor>>();
        _extractor = new TxtTextExtractor(_loggerMock.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidTxtFile_ReturnsTextContent()
    {
        // Arrange
        var testContent = "This is a test file.\nSecond line.\nThird line.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        var fileName = "test.txt";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.ShouldNotBeNull();
        result.Text.ShouldBe(testContent);
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidTxtFile_ExtractsMetadata()
    {
        // Arrange
        var testContent = "Sample content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        var fileName = "sample.txt";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Metadata.ShouldContainKey("OriginalFileName");
        result.Metadata["OriginalFileName"].ShouldBe(fileName);
        result.Metadata.ShouldContainKey("ExtractedAt");
        result.Metadata["ExtractedAt"].ShouldBeOfType<DateTime>();
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyTxtFile_ReturnsEmptyString()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
        var fileName = "empty.txt";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Text.ShouldBe(string.Empty);
        result.Metadata.ShouldContainKey("OriginalFileName");
    }

    [Fact]
    public async Task ExtractTextAsync_AfterExtraction_ResetsStreamPosition()
    {
        // Arrange
        var testContent = "Test content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        var fileName = "test.txt";

        // Act
        await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream? nullStream = null;
        var fileName = "test.txt";

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _extractor.ExtractTextAsync(nullStream!, fileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullFileName_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        string? nullFileName = null;

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _extractor.ExtractTextAsync(stream, nullFileName!));
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyFileName_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var emptyFileName = string.Empty;

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _extractor.ExtractTextAsync(stream, emptyFileName));
    }

    [Fact]
    public async Task ExtractTextAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var testContent = "Special chars: @#$%^&*()_+-={}[]|\\:\";<>?,./\nNew line test\tTab test";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        var fileName = "special.txt";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Text.ShouldBe(testContent);
    }

    [Fact]
    public async Task ExtractTextAsync_WithUnicodeContent_HandlesCorrectly()
    {
        // Arrange
        var testContent = "Українська мова: тестування екстракції тексту.\n中文測試\n日本語テスト";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        var fileName = "unicode.txt";

        // Act
        var result = await _extractor.ExtractTextAsync(stream, fileName);

        // Assert
        result.Text.ShouldBe(testContent);
    }
}
