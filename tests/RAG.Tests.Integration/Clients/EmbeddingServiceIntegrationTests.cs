using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Infrastructure.Clients;
using Shouldly;

namespace RAG.Tests.Integration.Clients;

public class EmbeddingServiceIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmbeddingService _embeddingService;
    private readonly bool _serviceAvailable;

    public EmbeddingServiceIntegrationTests()
    {
        // Setup DI container with real services
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole());

        // Configure EmbeddingServiceOptions
        services.Configure<EmbeddingServiceOptions>(options =>
        {
            options.ServiceUrl = "http://localhost:8001";
            options.TimeoutSeconds = 30;
            options.MaxBatchSize = 32;
            options.MaxRetries = 3;
        });

        // Register HttpClient with retry policies
        services.AddHttpClient<IEmbeddingService, EmbeddingServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        _serviceProvider = services.BuildServiceProvider();
        _embeddingService = _serviceProvider.GetRequiredService<IEmbeddingService>();

        // Check if service is available
        _serviceAvailable = CheckServiceAvailability().GetAwaiter().GetResult();
    }

    private async Task<bool> CheckServiceAvailability()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            var response = await httpClient.GetAsync("http://localhost:8001/");
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithSingleText_ReturnsValidEmbedding()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            // Log skip reason
            return;
        }

        // Arrange
        var texts = new List<string> { "Hello, world!" };

        // Act
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        embeddings.ShouldNotBeNull();
        embeddings.Count.ShouldBe(1);
        embeddings[0].Length.ShouldBe(384, "all-MiniLM-L6-v2 model produces 384-dimensional embeddings");

        // Verify embeddings contain actual float values (not all zeros)
        var hasNonZeroValues = embeddings[0].Any(v => v != 0.0f);
        hasNonZeroValues.ShouldBeTrue("Embedding should contain non-zero values");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithBatchOf10Texts_Returns10Embeddings()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            return;
        }

        // Arrange
        var texts = Enumerable.Range(1, 10).Select(i => $"Test sentence number {i}").ToList();

        // Act
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        embeddings.ShouldNotBeNull();
        embeddings.Count.ShouldBe(10);

        // Verify all embeddings have correct dimension
        foreach (var embedding in embeddings)
        {
            embedding.Length.ShouldBe(384);

            // Verify each embedding has non-zero values
            var hasNonZeroValues = embedding.Any(v => v != 0.0f);
            hasNonZeroValues.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithMaxBatchSize_ProcessesSuccessfully()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            return;
        }

        // Arrange
        var texts = Enumerable.Range(1, 32).Select(i => $"Sentence {i}").ToList();

        // Act
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        embeddings.ShouldNotBeNull();
        embeddings.Count.ShouldBe(32);

        // Verify all embeddings are valid
        foreach (var embedding in embeddings)
        {
            embedding.Length.ShouldBe(384);
        }
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithDifferentTexts_ProducesDifferentEmbeddings()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            return;
        }

        // Arrange
        var texts = new List<string>
        {
            "The quick brown fox jumps over the lazy dog",
            "Machine learning is a subset of artificial intelligence"
        };

        // Act
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        embeddings.Count.ShouldBe(2);

        // Verify embeddings are different (not identical)
        var areDifferent = false;
        for (int i = 0; i < 384; i++)
        {
            if (Math.Abs(embeddings[0][i] - embeddings[1][i]) > 0.001f)
            {
                areDifferent = true;
                break;
            }
        }
        areDifferent.ShouldBeTrue("Different texts should produce different embeddings");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithSameText_ProducesSimilarEmbeddings()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            return;
        }

        // Arrange
        var sameText = "Retrieval-Augmented Generation is a powerful technique";
        var texts = new List<string> { sameText, sameText };

        // Act
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        embeddings.Count.ShouldBe(2);

        // Verify embeddings are very similar (within floating-point tolerance)
        for (int i = 0; i < 384; i++)
        {
            Math.Abs(embeddings[0][i] - embeddings[1][i]).ShouldBeLessThan(0.0001f);
        }
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithServiceDown_ThrowsAfterRetries()
    {
        // Arrange - Create client with invalid URL
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.Configure<EmbeddingServiceOptions>(options =>
        {
            options.ServiceUrl = "http://localhost:9999";  // Invalid port
            options.TimeoutSeconds = 2;  // Short timeout for faster test
            options.MaxBatchSize = 32;
            options.MaxRetries = 3;
        });

        services.AddHttpClient<IEmbeddingService, EmbeddingServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        var sp = services.BuildServiceProvider();
        var service = sp.GetRequiredService<IEmbeddingService>();

        var texts = new List<string> { "test" };

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            async () => await service.GenerateEmbeddingsAsync(texts));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_Performance_CompletesWithinReasonableTime()
    {
        // Skip if service not available
        if (!_serviceAvailable)
        {
            return;
        }

        // Arrange
        var texts = Enumerable.Range(1, 10).Select(i => $"Performance test text {i}").ToList();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);
        stopwatch.Stop();

        // Assert
        embeddings.Count.ShouldBe(10);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000,
            "Generating 10 embeddings should take less than 5 seconds (expected ~500ms based on dev notes)");
    }
}
