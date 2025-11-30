using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Core.Domain;
using RAG.Infrastructure.Configuration;
using RAG.Infrastructure.LLMProviders;
using Shouldly;

namespace RAG.Tests.Unit.LLMProviders;

/// <summary>
/// Unit tests for OpenAIProvider.
/// Note: These tests verify behavior without actual OpenAI API calls.
/// </summary>
public class OpenAIProviderTests
{
    private readonly Mock<ILogger<OpenAIProvider>> _mockLogger;
    private readonly OpenAIOptions _options;

    public OpenAIProviderTests()
    {
        _mockLogger = new Mock<ILogger<OpenAIProvider>>();
        _options = new OpenAIOptions
        {
            ApiKey = "sk-test-key-1234567890",
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesProvider()
    {
        // Arrange
        var optionsMock = Options.Create(_options);

        // Act
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);

        // Assert
        provider.ShouldNotBeNull();
        provider.ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OpenAIProvider(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsMock = Options.Create(_options);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OpenAIProvider(optionsMock, null!));
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new OpenAIOptions
        {
            ApiKey = "",
            Model = "gpt-4-turbo"
        };
        var optionsMock = Options.Create(invalidOptions);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            new OpenAIProvider(optionsMock, _mockLogger.Object));

        ex.Message.ShouldContain("OpenAI API key is not configured");
    }

    [Fact]
    public void ProviderName_ReturnsOpenAI()
    {
        // Arrange
        var optionsMock = Options.Create(_options);
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);

        // Act
        var providerName = provider.ProviderName;

        // Assert
        providerName.ShouldBe("OpenAI");
    }

    [Fact]
    public void IsAvailable_WithValidApiKey_ReturnsTrue()
    {
        // Arrange
        var optionsMock = Options.Create(_options);
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);

        // Act
        var isAvailable = provider.IsAvailable;

        // Assert
        isAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsMock = Options.Create(_options);
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await provider.GenerateAsync(null!));
    }

    [Fact]
    public async Task GenerateStreamAsync_NotImplemented_ThrowsNotImplementedException()
    {
        // Arrange
        var optionsMock = Options.Create(_options);
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);
        var request = new GenerationRequest { Query = "test" };

        // Act & Assert
        var ex = await Should.ThrowAsync<NotImplementedException>(async () =>
            await provider.GenerateStreamAsync(request));

        ex.Message.ShouldContain("Story 5.7");
    }

    [Theory]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4-turbo-preview")]
    public void Constructor_WithDifferentModels_CreatesProvider(string model)
    {
        // Arrange
        _options.Model = model;
        var optionsMock = Options.Create(_options);

        // Act
        var provider = new OpenAIProvider(optionsMock, _mockLogger.Object);

        // Assert
        provider.ShouldNotBeNull();
        provider.ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new OpenAIOptions
        {
            ApiKey = "sk-test",
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };

        // Act & Assert
        Should.NotThrow(() => options.Validate());
    }

    [Fact]
    public void Validate_WithNegativeMaxRetries_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new OpenAIOptions
        {
            ApiKey = "sk-test",
            Model = "gpt-4-turbo",
            MaxRetries = -1,
            TimeoutSeconds = 60
        };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
        ex.Message.ShouldContain("MaxRetries");
    }

    [Fact]
    public void Validate_WithZeroTimeout_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new OpenAIOptions
        {
            ApiKey = "sk-test",
            Model = "gpt-4-turbo",
            MaxRetries = 3,
            TimeoutSeconds = 0
        };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
        ex.Message.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_WithEmptyModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new OpenAIOptions
        {
            ApiKey = "sk-test",
            Model = "",
            MaxRetries = 3,
            TimeoutSeconds = 60
        };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
        ex.Message.ShouldContain("Model");
    }
}
