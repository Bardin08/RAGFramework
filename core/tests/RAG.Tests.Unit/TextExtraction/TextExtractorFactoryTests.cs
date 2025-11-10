using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Exceptions;
using RAG.Infrastructure.TextExtraction;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.TextExtraction;

public class TextExtractorFactoryTests
{
    private readonly TextExtractorFactory _factory;
    private readonly IServiceProvider _serviceProvider;
    private readonly TxtTextExtractor _txtExtractor;
    private readonly PdfTextExtractor _pdfExtractor;
    private readonly DocxTextExtractor _docxExtractor;

    public TextExtractorFactoryTests()
    {
        _txtExtractor = new TxtTextExtractor(new Mock<ILogger<TxtTextExtractor>>().Object);
        _pdfExtractor = new PdfTextExtractor(new Mock<ILogger<PdfTextExtractor>>().Object);
        _docxExtractor = new DocxTextExtractor(new Mock<ILogger<DocxTextExtractor>>().Object);

        // Setup service provider mock
        var services = new ServiceCollection();
        services.AddSingleton(_txtExtractor);
        services.AddSingleton(_pdfExtractor);
        services.AddSingleton(_docxExtractor);
        _serviceProvider = services.BuildServiceProvider();

        _factory = new TextExtractorFactory(_serviceProvider);
    }

    [Fact]
    public void GetExtractor_ForTxtFile_ReturnsTxtTextExtractor()
    {
        // Arrange
        var fileName = "document.txt";

        // Act
        var extractor = _factory.GetExtractor(fileName);

        // Assert
        extractor.ShouldNotBeNull();
        extractor.ShouldBeOfType<TxtTextExtractor>();
    }

    [Fact]
    public void GetExtractor_ForPdfFile_ReturnsPdfTextExtractor()
    {
        // Arrange
        var fileName = "document.pdf";

        // Act
        var extractor = _factory.GetExtractor(fileName);

        // Assert
        extractor.ShouldNotBeNull();
        extractor.ShouldBeOfType<PdfTextExtractor>();
    }

    [Fact]
    public void GetExtractor_ForDocxFile_ReturnsDocxTextExtractor()
    {
        // Arrange
        var fileName = "report.docx";

        // Act
        var extractor = _factory.GetExtractor(fileName);

        // Assert
        extractor.ShouldNotBeNull();
        extractor.ShouldBeOfType<DocxTextExtractor>();
    }

    [Theory]
    [InlineData("file.TXT")]
    [InlineData("FILE.PDF")]
    [InlineData("Document.DOCX")]
    [InlineData("MixedCase.TxT")]
    [InlineData("UPPERCASE.PDF")]
    public void GetExtractor_WithMixedCaseExtensions_HandlesCaseInsensitively(string fileName)
    {
        // Act
        var extractor = _factory.GetExtractor(fileName);

        // Assert
        extractor.ShouldNotBeNull();
        extractor.ShouldBeAssignableTo<ITextExtractor>();
    }

    [Theory]
    [InlineData("file.exe")]
    [InlineData("document.zip")]
    [InlineData("image.png")]
    [InlineData("video.mp4")]
    [InlineData("archive.rar")]
    [InlineData("noextension")]
    [InlineData("file.unknown")]
    public void GetExtractor_ForUnsupportedFileType_ThrowsUnsupportedFileTypeException(string fileName)
    {
        // Act & Assert
        Should.Throw<UnsupportedFileTypeException>(() => _factory.GetExtractor(fileName));
    }

    [Fact]
    public void GetExtractor_WithNullFileName_ThrowsArgumentException()
    {
        // Arrange
        string? nullFileName = null;

        // Act & Assert
        Should.Throw<ArgumentException>(() => _factory.GetExtractor(nullFileName!));
    }

    [Fact]
    public void GetExtractor_WithEmptyFileName_ThrowsArgumentException()
    {
        // Arrange
        var emptyFileName = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => _factory.GetExtractor(emptyFileName));
    }

    [Fact]
    public void GetExtractor_WithWhitespaceFileName_ThrowsArgumentException()
    {
        // Arrange
        var whitespaceFileName = "   ";

        // Act & Assert
        Should.Throw<ArgumentException>(() => _factory.GetExtractor(whitespaceFileName));
    }

    [Theory]
    [InlineData("path/to/document.txt")]
    [InlineData("C:\\Users\\Documents\\file.pdf")]
    [InlineData("../relative/path/report.docx")]
    [InlineData("/absolute/path/file.txt")]
    public void GetExtractor_WithFullPath_ExtractsExtensionCorrectly(string filePath)
    {
        // Act
        var extractor = _factory.GetExtractor(filePath);

        // Assert
        extractor.ShouldNotBeNull();
        extractor.ShouldBeAssignableTo<ITextExtractor>();
    }

    [Fact]
    public void GetExtractor_WithFileNameContainingMultipleDots_UsesLastExtension()
    {
        // Arrange
        var fileName = "my.document.backup.txt";

        // Act
        var extractor = _factory.GetExtractor(fileName);

        // Assert
        extractor.ShouldBeOfType<TxtTextExtractor>();
    }
}
