using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RAG.API.Models.Requests;
using RAG.API.Validators;
using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.Validators;

public class DocumentUploadRequestValidatorTests
{
    private readonly DocumentUploadRequestValidator _validator;
    private readonly ValidationSettings _settings;

    public DocumentUploadRequestValidatorTests()
    {
        _settings = new ValidationSettings
        {
            MaxFileSizeMb = 50,
            AllowedFileExtensions = [".txt", ".pdf", ".docx"],
            MaxTitleLength = 500
        };
        _validator = new DocumentUploadRequestValidator(Options.Create(_settings));
    }

    private static IFormFile CreateMockFile(string fileName, long size)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(size);
        fileMock.Setup(f => f.ContentType).Returns("application/octet-stream");
        return fileMock.Object;
    }

    [Fact]
    public async Task Validate_NullFile_ShouldHaveError()
    {
        // Arrange
        var request = new DocumentUploadRequest { File = null! };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File)
            .WithErrorMessage("File is required");
    }

    [Fact]
    public async Task Validate_FileTooLarge_ShouldHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", _settings.MaxFileSizeBytes + 1);
        var request = new DocumentUploadRequest { File = file };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File)
            .WithErrorMessage($"File size cannot exceed {_settings.MaxFileSizeMb}MB");
    }

    [Fact]
    public async Task Validate_FileAtMaxSize_ShouldNotHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", _settings.MaxFileSizeBytes);
        var request = new DocumentUploadRequest { File = file };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.File);
    }

    [Theory]
    [InlineData("test.txt")]
    [InlineData("document.pdf")]
    [InlineData("report.docx")]
    [InlineData("TEST.TXT")]
    [InlineData("Document.PDF")]
    public async Task Validate_AllowedFileExtension_ShouldNotHaveError(string fileName)
    {
        // Arrange
        var file = CreateMockFile(fileName, 1024);
        var request = new DocumentUploadRequest { File = file };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.File);
    }

    [Theory]
    [InlineData("test.exe")]
    [InlineData("script.js")]
    [InlineData("image.png")]
    [InlineData("archive.zip")]
    public async Task Validate_DisallowedFileExtension_ShouldHaveError(string fileName)
    {
        // Arrange
        var file = CreateMockFile(fileName, 1024);
        var request = new DocumentUploadRequest { File = file };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File)
            .WithErrorMessage($"Allowed file types: {string.Join(", ", _settings.AllowedFileExtensions)}");
    }

    [Fact]
    public async Task Validate_TitleTooLong_ShouldHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", 1024);
        var request = new DocumentUploadRequest
        {
            File = file,
            Title = new string('a', _settings.MaxTitleLength + 1)
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage($"Title cannot exceed {_settings.MaxTitleLength} characters");
    }

    [Fact]
    public async Task Validate_ValidTitle_ShouldNotHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", 1024);
        var request = new DocumentUploadRequest
        {
            File = file,
            Title = "My Document Title"
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public async Task Validate_NullTitle_ShouldNotHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", 1024);
        var request = new DocumentUploadRequest
        {
            File = file,
            Title = null
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public async Task Validate_SourceTooLong_ShouldHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", 1024);
        var request = new DocumentUploadRequest
        {
            File = file,
            Source = new string('a', 1001)
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source cannot exceed 1000 characters");
    }

    [Fact]
    public async Task Validate_ValidSource_ShouldNotHaveError()
    {
        // Arrange
        var file = CreateMockFile("test.pdf", 1024);
        var request = new DocumentUploadRequest
        {
            File = file,
            Source = "https://example.com/document"
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Source);
    }

    [Fact]
    public async Task Validate_CompleteValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var file = CreateMockFile("report.pdf", 1024 * 1024); // 1MB
        var request = new DocumentUploadRequest
        {
            File = file,
            Title = "Annual Report 2024",
            Source = "Finance Department"
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_MinimalValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var file = CreateMockFile("document.txt", 100);
        var request = new DocumentUploadRequest { File = file };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
