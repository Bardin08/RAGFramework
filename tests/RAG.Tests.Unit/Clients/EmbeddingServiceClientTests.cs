using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Infrastructure.Clients;
using Shouldly;

namespace RAG.Tests.Unit.Clients;

public class EmbeddingServiceClientTests
{
    private readonly Mock<ILogger<EmbeddingServiceClient>> _loggerMock;
    private readonly EmbeddingServiceOptions _defaultOptions;

    public EmbeddingServiceClientTests()
    {
        _loggerMock = new Mock<ILogger<EmbeddingServiceClient>>();
        _defaultOptions = new EmbeddingServiceOptions
        {
            ServiceUrl = "http://localhost:8001",
            TimeoutSeconds = 30,
            MaxBatchSize = 32,
            MaxRetries = 3
        };
    }

    private EmbeddingServiceClient CreateClient(
        HttpMessageHandler? handler = null,
        EmbeddingServiceOptions? options = null)
    {
        var optionsToUse = options ?? _defaultOptions;
        var optionsMock = new Mock<IOptions<EmbeddingServiceOptions>>();
        optionsMock.Setup(o => o.Value).Returns(optionsToUse);

        var httpClient = handler != null
            ? new HttpClient(handler) { BaseAddress = new Uri(optionsToUse.ServiceUrl) }
            : new HttpClient() { BaseAddress = new Uri(optionsToUse.ServiceUrl) };

        return new EmbeddingServiceClient(httpClient, optionsMock.Object, _loggerMock.Object);
    }

    private Mock<HttpMessageHandler> CreateMockHttpHandler(HttpStatusCode statusCode, string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });
        return mockHandler;
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithValidResponse_ReturnsEmbeddings()
    {
        // Arrange
        var texts = new List<string> { "text1", "text2", "text3" };
        var expectedEmbeddings = new List<float[]>
        {
            new float[384],
            new float[384],
            new float[384]
        };
        var response = new EmbeddingResponse(expectedEmbeddings);
        var responseJson = JsonSerializer.Serialize(response);

        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, responseJson);
        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateEmbeddingsAsync(texts);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result[0].Length.ShouldBe(384);
        result[1].Length.ShouldBe(384);
        result[2].Length.ShouldBe(384);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithNullTexts_ThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await client.GenerateEmbeddingsAsync(null!));
        exception.ParamName.ShouldBe("texts");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmptyTexts_ThrowsArgumentException()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await client.GenerateEmbeddingsAsync(new List<string>()));
        exception.ParamName.ShouldBe("texts");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ExceedingMaxBatchSize_ThrowsArgumentException()
    {
        // Arrange
        var client = CreateClient();
        var texts = Enumerable.Range(1, 33).Select(i => $"text{i}").ToList(); // 33 texts (max is 32)

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await client.GenerateEmbeddingsAsync(texts));
        exception.ParamName.ShouldBe("texts");
        exception.Message.ShouldContain("Batch size exceeds maximum limit");
        exception.Message.ShouldContain("33");
        exception.Message.ShouldContain("32");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithHttpError500_ThrowsHttpRequestException()
    {
        // Arrange
        var texts = new List<string> { "text1" };
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.InternalServerError, "Server error");
        var client = CreateClient(mockHandler.Object);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            async () => await client.GenerateEmbeddingsAsync(texts));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithHttpError503_ThrowsHttpRequestException()
    {
        // Arrange
        var texts = new List<string> { "text1" };
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.ServiceUnavailable, "Service unavailable");
        var client = CreateClient(mockHandler.Object);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            async () => await client.GenerateEmbeddingsAsync(texts));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmbeddingCountMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        var texts = new List<string> { "text1", "text2", "text3" };
        var wrongEmbeddings = new List<float[]>
        {
            new float[384],
            new float[384]  // Only 2 embeddings instead of 3
        };
        var response = new EmbeddingResponse(wrongEmbeddings);
        var responseJson = JsonSerializer.Serialize(response);

        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, responseJson);
        var client = CreateClient(mockHandler.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await client.GenerateEmbeddingsAsync(texts));
        exception.Message.ShouldContain("Embedding count mismatch");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithTimeout_ThrowsTaskCanceledException()
    {
        // Arrange
        var texts = new List<string> { "text1" };
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var client = CreateClient(mockHandler.Object);

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(
            async () => await client.GenerateEmbeddingsAsync(texts));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_VerifiesRequestFormat()
    {
        // Arrange
        var texts = new List<string> { "hello", "world" };
        var expectedEmbeddings = new List<float[]> { new float[384], new float[384] };
        var response = new EmbeddingResponse(expectedEmbeddings);
        var responseJson = JsonSerializer.Serialize(response);

        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        await client.GenerateEmbeddingsAsync(texts);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri?.PathAndQuery.ShouldBe("/embed");

        var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
        var requestObj = JsonSerializer.Deserialize<EmbeddingRequest>(requestContent);
        requestObj.ShouldNotBeNull();
        requestObj.Texts.Count.ShouldBe(2);
        requestObj.Texts[0].ShouldBe("hello");
        requestObj.Texts[1].ShouldBe("world");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_LogsSuccessfully()
    {
        // Arrange
        var texts = new List<string> { "text1" };
        var expectedEmbeddings = new List<float[]> { new float[384] };
        var response = new EmbeddingResponse(expectedEmbeddings);
        var responseJson = JsonSerializer.Serialize(response);

        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, responseJson);
        var client = CreateClient(mockHandler.Object);

        // Act
        await client.GenerateEmbeddingsAsync(texts);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generating embeddings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully generated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_LogsErrorOnTimeout()
    {
        // Arrange
        var texts = new List<string> { "text1" };
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var client = CreateClient(mockHandler.Object);

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(
            async () => await client.GenerateEmbeddingsAsync(texts));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithInvalidConfiguration_ThrowsOnValidation()
    {
        // Arrange
        var invalidOptions = new EmbeddingServiceOptions
        {
            ServiceUrl = "http://localhost:8001",  // Valid URL needed for HttpClient creation
            TimeoutSeconds = -1,  // Invalid
            MaxBatchSize = 0,  // Invalid
            MaxRetries = -1  // Invalid
        };

        // Act & Assert
        // Validation happens in constructor, so creating client should throw
        Should.Throw<InvalidOperationException>(() => CreateClient(options: invalidOptions));
    }
}
