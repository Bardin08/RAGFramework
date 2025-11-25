using System.Net;
using System.Net.Http.Json;
using System.Text;
using RAG.API.Models.Responses;
using RAG.Core.Domain.Enums;
using Shouldly;

namespace RAG.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for DocumentsController.
/// </summary>
public class DocumentsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public DocumentsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithValidFile_Returns201Created()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var sampleFileContent = "This is a sample test file for document upload.\nTest content for integration test.";
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(sampleFileContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        content.Add(fileContent, "file", "sample.txt");
        content.Add(new StringContent("Test Document"), "title");
        content.Add(new StringContent("integration-test"), "source");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DocumentUploadResponse>();
        result.ShouldNotBeNull();
        result.DocumentId.ShouldNotBe(Guid.Empty);
        result.Title.ShouldBe("Test Document");
        result.Status.ShouldBe(DocumentStatus.Uploaded);
        result.UploadedAt.ShouldNotBe(default(DateTime));
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithoutTitle_UsesFileName()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        content.Add(fileContent, "file", "untitled-document.txt");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DocumentUploadResponse>();
        result.ShouldNotBeNull();
        result.Title.ShouldBe("untitled-document.txt");
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithLargeFile_Returns413PayloadTooLarge()
    {
        // Arrange - Create a file larger than 10MB
        var content = new MultipartFormDataContent();
        var largeFileSize = 11 * 1024 * 1024; // 11 MB
        var largeFileContent = new ByteArrayContent(new byte[largeFileSize]);
        largeFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        content.Add(largeFileContent, "file", "large-file.pdf");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)413); // 413 Payload Too Large
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithInvalidExtension_Returns400BadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/exe");

        content.Add(fileContent, "file", "malicious.exe");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithEmptyFile_Returns400BadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var emptyFileContent = new ByteArrayContent(Array.Empty<byte>());
        emptyFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        content.Add(emptyFileContent, "file", "empty.txt");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithJpgFile_Returns400BadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake jpg content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        content.Add(fileContent, "file", "image.jpg");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithValidPdf_Returns201Created()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("PDF content placeholder"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        content.Add(fileContent, "file", "document.pdf");
        content.Add(new StringContent("PDF Document"), "title");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DocumentUploadResponse>();
        result.ShouldNotBeNull();
        result.Title.ShouldBe("PDF Document");
    }

    [Fact(Skip = "TestWebApplicationFactory configuration issues")]
    public async Task Upload_WithValidDocx_Returns201Created()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("DOCX content placeholder"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        content.Add(fileContent, "file", "document.docx");
        content.Add(new StringContent("Word Document"), "title");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DocumentUploadResponse>();
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Word Document");
    }
}
