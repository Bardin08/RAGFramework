using RAG.Application.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for FileValidationService.
/// </summary>
public class FileValidationServiceTests
{
    private readonly FileValidationService _service;

    public FileValidationServiceTests()
    {
        _service = new FileValidationService();
    }

    [Fact]
    public void ValidateFile_WithValidTxtFile_ReturnsSuccess()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
        var fileName = "document.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateFile_WithValidPdfFile_ReturnsSuccess()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("PDF content"));
        var fileName = "document.pdf";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateFile_WithValidDocxFile_ReturnsSuccess()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("DOCX content"));
        var fileName = "document.docx";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateFile_WithFileTooLarge_ReturnsFailure()
    {
        // Arrange
        var largeSize = 11 * 1024 * 1024; // 11 MB
        using var stream = new MemoryStream(new byte[largeSize]);
        var fileName = "large-document.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("exceeds maximum"));
    }

    [Fact]
    public void ValidateFile_WithInvalidExtension_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
        var fileName = "document.exe";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("not allowed"));
    }

    [Fact]
    public void ValidateFile_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();
        var fileName = "empty.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("cannot be empty"));
    }

    [Fact]
    public void ValidateFile_WithNullFile_ReturnsFailure()
    {
        // Arrange
        Stream? stream = null;
        var fileName = "document.txt";

        // Act
        var result = _service.ValidateFile(stream!, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("required"));
    }

    [Fact]
    public void ValidateFile_WithPathTraversalInFileName_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
        var fileName = "../../../etc/passwd.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("Invalid file name"));
    }

    [Fact]
    public void ValidateFile_WithJpgExtension_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Image content"));
        var fileName = "photo.jpg";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("not allowed"));
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".txt")]
    public void ValidateFile_WithAllowedExtensions_ReturnsSuccess(string extension)
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
        var fileName = $"document{extension}";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".zip")]
    [InlineData(".rar")]
    public void ValidateFile_WithDisallowedExtensions_ReturnsFailure(string extension)
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Test content"));
        var fileName = $"document{extension}";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("not allowed"));
    }

    [Fact]
    public void ValidateFile_WithExactly10MB_ReturnsSuccess()
    {
        // Arrange
        var exactSize = 10 * 1024 * 1024; // Exactly 10 MB
        using var stream = new MemoryStream(new byte[exactSize]);
        var fileName = "document.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateFile_WithSlightlyOver10MB_ReturnsFailure()
    {
        // Arrange
        var slightlyOverSize = (10 * 1024 * 1024) + 1; // 10 MB + 1 byte
        using var stream = new MemoryStream(new byte[slightlyOverSize]);
        var fileName = "document.txt";

        // Act
        var result = _service.ValidateFile(stream, fileName);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("exceeds maximum"));
    }
}
