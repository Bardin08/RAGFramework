using Moq;
using RAG.Core.Domain;
using RAG.Core.Interfaces;
using Shouldly;

namespace RAG.Tests.Unit.Interfaces;

/// <summary>
/// Tests for ILLMProvider interface contract compliance.
/// </summary>
public class ILLMProviderTests
{
    private readonly Mock<ILLMProvider> _mockProvider;

    public ILLMProviderTests()
    {
        _mockProvider = new Mock<ILLMProvider>();
    }

    [Fact]
    public async Task GenerateAsync_WithValidRequest_ReturnsGenerationResponse()
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "What is RAG?",
            Context = "RAG stands for Retrieval-Augmented Generation",
            MaxTokens = 500,
            Temperature = 0.7m
        };

        var expectedResponse = new GenerationResponse(
            Answer: "RAG is Retrieval-Augmented Generation",
            Model: "test-model",
            TokensUsed: 50,
            ResponseTime: TimeSpan.FromSeconds(1),
            Sources: new List<SourceReference>()
        );

        _mockProvider
            .Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockProvider.Object.GenerateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Answer.ShouldBe("RAG is Retrieval-Augmented Generation");
        result.Model.ShouldBe("test-model");
        result.TokensUsed.ShouldBe(50);
        result.Sources.ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_SupportsCancellationToken()
    {
        // Arrange
        var request = new GenerationRequest { Query = "test" };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var response = new GenerationResponse(
            Answer: "test",
            Model: "test",
            TokensUsed: 10,
            ResponseTime: TimeSpan.FromSeconds(1),
            Sources: new List<SourceReference>()
        );

        _mockProvider
            .Setup(p => p.GenerateAsync(request, cancellationToken))
            .ReturnsAsync(response);

        // Act
        var result = await _mockProvider.Object.GenerateAsync(request, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        _mockProvider.Verify(p => p.GenerateAsync(request, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GenerateStreamAsync_ReturnsAsyncEnumerable()
    {
        // Arrange
        var request = new GenerationRequest { Query = "test" };
        var tokens = new List<string> { "Hello", " ", "World" };

        async IAsyncEnumerable<string> GetTokensAsync()
        {
            foreach (var token in tokens)
            {
                await Task.Delay(10);
                yield return token;
            }
        }

        _mockProvider
            .Setup(p => p.GenerateStreamAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetTokensAsync());

        // Act
        var streamTask = await _mockProvider.Object.GenerateStreamAsync(request);
        var result = new List<string>();
        await foreach (var token in streamTask)
        {
            result.Add(token);
        }

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldBe(tokens);
    }

    [Fact]
    public void ProviderName_ReturnsNonEmptyString()
    {
        // Arrange
        _mockProvider.Setup(p => p.ProviderName).Returns("TestProvider");

        // Act
        var providerName = _mockProvider.Object.ProviderName;

        // Assert
        providerName.ShouldNotBeNullOrWhiteSpace();
        providerName.ShouldBe("TestProvider");
    }

    [Fact]
    public void IsAvailable_ReturnsBoolean()
    {
        // Arrange
        _mockProvider.Setup(p => p.IsAvailable).Returns(true);

        // Act
        var isAvailable = _mockProvider.Object.IsAvailable;

        // Assert
        isAvailable.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsAvailable_CanReturnBothStates(bool availabilityState)
    {
        // Arrange
        _mockProvider.Setup(p => p.IsAvailable).Returns(availabilityState);

        // Act
        var isAvailable = _mockProvider.Object.IsAvailable;

        // Assert
        isAvailable.ShouldBe(availabilityState);
    }

    [Fact]
    public async Task GenerateAsync_WithCancellationRequested_CanBeCancelled()
    {
        // Arrange
        var request = new GenerationRequest { Query = "test" };
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockProvider
            .Setup(p => p.GenerateAsync(It.IsAny<GenerationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _mockProvider.Object.GenerateAsync(request, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GenerateStreamAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        var request = new GenerationRequest { Query = "test" };
        var cancellationTokenSource = new CancellationTokenSource();

        async IAsyncEnumerable<string> GetTokensAsync()
        {
            yield return "token1";
            cancellationTokenSource.Cancel();
            await Task.CompletedTask;
        }

        _mockProvider
            .Setup(p => p.GenerateStreamAsync(request, cancellationTokenSource.Token))
            .ReturnsAsync(GetTokensAsync());

        // Act
        var streamTask = await _mockProvider.Object.GenerateStreamAsync(request, cancellationTokenSource.Token);
        var result = new List<string>();

        await foreach (var token in streamTask)
        {
            result.Add(token);
        }

        // Assert
        result.Count.ShouldBeGreaterThanOrEqualTo(1);
    }
}
