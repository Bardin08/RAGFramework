using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RAG.Core.Domain;
using RAG.Infrastructure.Configuration;
using RAG.Infrastructure.LLMProviders;
using Shouldly;
using System.Net;
using System.Text;
using Xunit;

namespace RAG.Tests.Unit.LLMProviders;

public class OllamaProviderTests
{
    private readonly Mock<ILogger<OllamaProvider>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly OllamaOptions _options;

    public OllamaProviderTests()
    {
        _mockLogger = new Mock<ILogger<OllamaProvider>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _options = new OllamaOptions
        {
            BaseUrl = "http://localhost:11434",
            Model = "llama3.1:8b",
            TimeoutSeconds = 60,
            StreamingEnabled = true
        };

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OllamaProvider(null!, _mockLogger.Object, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_options);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OllamaProvider(options, null!, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new OllamaOptions
        {
            BaseUrl = "", // Invalid
            Model = "llama3.1:8b"
        };
        var options = Options.Create(invalidOptions);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new OllamaProvider(options, _mockLogger.Object, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void ProviderName_ShouldReturnOllama()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var name = provider.ProviderName;

        // Assert
        name.ShouldBe("Ollama");
    }

    [Fact]
    public void IsAvailable_WhenServiceIsHealthy_AndModelExists_ReturnsTrue()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" },
                    { "name": "phi-3:mini" }
                ]
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse);
        var provider = CreateProvider();

        // Act
        var isAvailable = provider.IsAvailable;

        // Assert
        isAvailable.ShouldBeTrue();
    }

    [Fact]
    public void IsAvailable_WhenModelNotFound_ReturnsFalse()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "phi-3:mini" }
                ]
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse);
        var provider = CreateProvider();

        // Act
        var isAvailable = provider.IsAvailable;

        // Assert
        isAvailable.ShouldBeFalse();
    }

    [Fact]
    public void IsAvailable_WhenServiceUnreachable_ReturnsFalse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.ServiceUnavailable, "");
        var provider = CreateProvider();

        // Act
        var isAvailable = provider.IsAvailable;

        // Assert
        isAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithValidRequest_ReturnsGenerationResponse()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" }
                ]
            }
            """;

        var generateResponse = """
            {
                "model": "llama3.1:8b",
                "created_at": "2024-01-01T00:00:00Z",
                "response": "This is a test response from Llama.",
                "done": true,
                "prompt_eval_count": 50,
                "eval_count": 20,
                "total_duration": 1000000000,
                "load_duration": 100000000,
                "prompt_eval_duration": 500000000,
                "eval_duration": 400000000
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse, "/api/tags");
        SetupHttpResponse(HttpStatusCode.OK, generateResponse, "/api/generate");

        var provider = CreateProvider();
        var request = new GenerationRequest
        {
            Query = "What is machine learning?",
            Context = "Machine learning is a subset of AI.",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        // Act
        var response = await provider.GenerateAsync(request);

        // Assert
        response.ShouldNotBeNull();
        response.Answer.ShouldBe("This is a test response from Llama.");
        response.Model.ShouldBe("llama3.1:8b");
        response.TokensUsed.ShouldBe(70); // 50 + 20
        response.ResponseTime.ShouldBeGreaterThan(TimeSpan.Zero);
        response.Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await provider.GenerateAsync(null!));
    }

    [Fact]
    public async Task GenerateAsync_WithHttpError_ThrowsHttpRequestException()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" }
                ]
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse, "/api/tags");
        SetupHttpResponse(HttpStatusCode.InternalServerError, "", "/api/generate");

        var provider = CreateProvider();
        var request = new GenerationRequest
        {
            Query = "What is machine learning?"
        };

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await provider.GenerateAsync(request));
    }

    [Fact]
    public async Task GenerateAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" }
                ]
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse, "/api/tags");

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/api/generate")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var provider = CreateProvider();
        var request = new GenerationRequest
        {
            Query = "What is machine learning?"
        };

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(async () =>
            await provider.GenerateAsync(request));
    }

    [Fact]
    public async Task GenerateStreamAsync_WithValidRequest_ReturnsAsyncEnumerable()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" }
                ]
            }
            """;

        var streamResponse = """
            {"model":"llama3.1:8b","created_at":"2024-01-01T00:00:00Z","response":"This ","done":false}
            {"model":"llama3.1:8b","created_at":"2024-01-01T00:00:01Z","response":"is ","done":false}
            {"model":"llama3.1:8b","created_at":"2024-01-01T00:00:02Z","response":"streaming.","done":true}
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse, "/api/tags");
        SetupStreamingHttpResponse(streamResponse, "/api/generate");

        var provider = CreateProvider();
        var request = new GenerationRequest
        {
            Query = "What is machine learning?",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        // Act
        var streamEnumerable = await provider.GenerateStreamAsync(request);
        var chunks = new List<string>();

        await foreach (var chunk in streamEnumerable)
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.ShouldNotBeEmpty();
        chunks.Count.ShouldBe(3);
        chunks[0].ShouldBe("This ");
        chunks[1].ShouldBe("is ");
        chunks[2].ShouldBe("streaming.");
    }

    [Fact]
    public async Task GenerateStreamAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await provider.GenerateStreamAsync(null!));
    }

    [Fact]
    public async Task GenerateAsync_BuildsCorrectPrompt_WithAllFields()
    {
        // Arrange
        var tagsResponse = """
            {
                "models": [
                    { "name": "llama3.1:8b" }
                ]
            }
            """;

        var generateResponse = """
            {
                "model": "llama3.1:8b",
                "response": "Answer",
                "done": true,
                "prompt_eval_count": 10,
                "eval_count": 5
            }
            """;

        SetupHttpResponse(HttpStatusCode.OK, tagsResponse, "/api/tags");

        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/api/generate")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(generateResponse, Encoding.UTF8, "application/json")
            });

        var provider = CreateProvider();
        var request = new GenerationRequest
        {
            Query = "What is AI?",
            Context = "AI is artificial intelligence.",
            SystemPrompt = "You are an expert.",
            MaxTokens = 100,
            Temperature = 0.7m
        };

        // Act
        await provider.GenerateAsync(request);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
        requestContent.ShouldContain("You are an expert.");
        requestContent.ShouldContain("Context:\\nAI is artificial intelligence.");
        requestContent.ShouldContain("Question: What is AI?");
        requestContent.ShouldContain("Answer:");
    }

    private OllamaProvider CreateProvider()
    {
        var options = Options.Create(_options);
        return new OllamaProvider(options, _mockLogger.Object, _mockHttpClientFactory.Object);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content, string? path = null)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        if (path == null)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }
        else
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains(path)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }
    }

    private void SetupStreamingHttpResponse(string streamContent, string path)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(streamContent, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains(path)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
