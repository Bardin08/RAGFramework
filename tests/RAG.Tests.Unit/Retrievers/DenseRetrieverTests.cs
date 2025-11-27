using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Unit.Retrievers;

public class DenseRetrieverTests
{
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorStoreClient> _vectorStoreClientMock;
    private readonly Mock<ILogger<DenseRetriever>> _loggerMock;
    private readonly DenseSettings _denseSettings;

    public DenseRetrieverTests()
    {
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorStoreClientMock = new Mock<IVectorStoreClient>();
        _loggerMock = new Mock<ILogger<DenseRetriever>>();

        _denseSettings = new DenseSettings
        {
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 10,
            SimilarityThreshold = 0.5,
            EmbeddingTimeoutSeconds = 5,
            QdrantTimeoutSeconds = 5
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<DenseSettings>? nullSettings = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DenseRetriever(nullSettings!, _embeddingServiceMock.Object, _vectorStoreClientMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullEmbeddingService_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_denseSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DenseRetriever(options, null!, _vectorStoreClientMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullVectorStoreClient_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_denseSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DenseRetriever(options, _embeddingServiceMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_denseSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DenseRetriever(options, _embeddingServiceMock.Object, _vectorStoreClientMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithInvalidDenseSettings_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = new DenseSettings { DefaultTopK = -1 }; // Invalid DefaultTopK
        var options = Options.Create(invalidSettings);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new DenseRetriever(options, _embeddingServiceMock.Object, _vectorStoreClientMock.Object, _loggerMock.Object));
    }

    #endregion

    #region SearchAsync Input Validation Tests

    [Fact]
    public async Task SearchAsync_WithNullQuery_ThrowsArgumentException()
    {
        // Arrange
        var retriever = CreateRetriever();
        string? nullQuery = null;
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync(nullQuery!, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var emptyQuery = "";
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync(emptyQuery, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceQuery_ThrowsArgumentException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var whitespaceQuery = "   ";
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync(whitespaceQuery, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithZeroTopK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 0;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithNegativeTopK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = -5;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithTopKExceedingMax_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 101; // Exceeds MaxTopK of 100
        var tenantId = Guid.NewGuid();

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));

        ex.Message.ShouldContain("100");
    }

    #endregion

    #region DenseSettings Validation Tests

    [Fact]
    public void DenseSettings_Validate_WithNegativeDefaultTopK_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { DefaultTopK = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithZeroDefaultTopK_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { DefaultTopK = 0 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithNegativeMaxTopK_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { MaxTopK = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithDefaultTopKExceedingMaxTopK_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings
        {
            DefaultTopK = 150,
            MaxTopK = 100
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithNegativeTimeoutSeconds_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { TimeoutSeconds = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithSimilarityThresholdBelowZero_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { SimilarityThreshold = -0.1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithSimilarityThresholdAboveOne_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { SimilarityThreshold = 1.1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithNegativeEmbeddingTimeoutSeconds_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { EmbeddingTimeoutSeconds = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithNegativeQdrantTimeoutSeconds_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new DenseSettings { QdrantTimeoutSeconds = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_Validate_WithValidSettings_DoesNotThrow()
    {
        // Arrange
        var settings = new DenseSettings
        {
            DefaultTopK = 20,
            MaxTopK = 100,
            TimeoutSeconds = 10,
            SimilarityThreshold = 0.7,
            EmbeddingTimeoutSeconds = 5,
            QdrantTimeoutSeconds = 5
        };

        // Act & Assert
        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void DenseSettings_DefaultValues_AreValid()
    {
        // Arrange
        var settings = new DenseSettings();

        // Act & Assert
        settings.DefaultTopK.ShouldBe(10);
        settings.MaxTopK.ShouldBe(100);
        settings.TimeoutSeconds.ShouldBe(10);
        settings.SimilarityThreshold.ShouldBe(0.5);
        settings.EmbeddingTimeoutSeconds.ShouldBe(5);
        settings.QdrantTimeoutSeconds.ShouldBe(5);
        Should.NotThrow(() => settings.Validate());
    }

    #endregion

    #region SearchAsync Successful Execution Tests

    [Fact]
    public async Task SearchAsync_WithValidInputs_ReturnsResults()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var documentId = Guid.NewGuid();

        var searchResults = new List<(Guid Id, double Score, Dictionary<string, object> Payload)>
        {
            (documentId, 0.8, new Dictionary<string, object>
            {
                { "text", "Sample text content" },
                { "documentId", documentId.ToString() },
                { "source", "test.pdf" }
            })
        };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.Is<List<string>>(list => list.Count == 1 && list[0] == query),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { queryEmbedding });

        _vectorStoreClientMock
            .Setup(x => x.SearchAsync(
                queryEmbedding,
                topK,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(1);
        results[0].DocumentId.ShouldBe(documentId);
        results[0].Text.ShouldBe("Sample text content");
        results[0].Source.ShouldBe("test.pdf");
        results[0].HighlightedText.ShouldBeNull(); // Dense retrieval doesn't support highlighting
    }

    [Fact]
    public async Task SearchAsync_NormalizesCosineSimilarityScore_Correctly()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 3;
        var tenantId = Guid.NewGuid();
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        var searchResults = new List<(Guid Id, double Score, Dictionary<string, object> Payload)>
        {
            (Guid.NewGuid(), 1.0, new Dictionary<string, object> { { "text", "identical" }, { "source", "doc1" } }),      // cosine=1.0 -> normalized=1.0
            (Guid.NewGuid(), 0.0, new Dictionary<string, object> { { "text", "orthogonal" }, { "source", "doc2" } }),     // cosine=0.0 -> normalized=0.5
            (Guid.NewGuid(), -1.0, new Dictionary<string, object> { { "text", "opposite" }, { "source", "doc3" } })       // cosine=-1.0 -> normalized=0.0
        };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { queryEmbedding });

        _vectorStoreClientMock
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.Count.ShouldBe(2); // -1.0 score filtered out by threshold (0.0 < 0.5)
        results[0].Score.ShouldBe(1.0, tolerance: 0.001);  // (1.0 + 1.0) / 2.0 = 1.0
        results[1].Score.ShouldBe(0.5, tolerance: 0.001);  // (0.0 + 1.0) / 2.0 = 0.5
    }

    [Fact]
    public async Task SearchAsync_FiltersBySimiiliarityThreshold_Correctly()
    {
        // Arrange
        var customSettings = new DenseSettings { SimilarityThreshold = 0.7 }; // Higher threshold
        var retriever = CreateRetriever(customSettings);
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        var searchResults = new List<(Guid Id, double Score, Dictionary<string, object> Payload)>
        {
            (Guid.NewGuid(), 0.9, new Dictionary<string, object> { { "text", "high similarity" }, { "source", "doc1" } }),     // normalized=0.95 -> included
            (Guid.NewGuid(), 0.5, new Dictionary<string, object> { { "text", "medium similarity" }, { "source", "doc2" } }),   // normalized=0.75 -> included
            (Guid.NewGuid(), 0.2, new Dictionary<string, object> { { "text", "low similarity" }, { "source", "doc3" } })       // normalized=0.6 -> filtered
        };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { queryEmbedding });

        _vectorStoreClientMock
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.Count.ShouldBe(2); // Only scores >= 0.7 threshold
        results[0].Text.ShouldBe("high similarity");
        results[1].Text.ShouldBe("medium similarity");
    }

    [Fact]
    public async Task SearchAsync_HandlesEmptyPayloadFields_Gracefully()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var documentId = Guid.NewGuid();

        var searchResults = new List<(Guid Id, double Score, Dictionary<string, object> Payload)>
        {
            (documentId, 0.8, new Dictionary<string, object>()) // Empty payload
        };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { queryEmbedding });

        _vectorStoreClientMock
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.Count.ShouldBe(1);
        results[0].DocumentId.ShouldBe(documentId);
        results[0].Text.ShouldBe(string.Empty); // Gracefully defaults to empty
        results[0].Source.ShouldBe("Unknown"); // Gracefully defaults to Unknown
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SearchAsync_WhenEmbeddingServiceThrowsHttpRequestException_ThrowsInvalidOperationException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Embedding service unavailable"));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));

        ex.Message.ShouldContain("External service unavailable");
    }

    [Fact]
    public async Task SearchAsync_WhenEmbeddingServiceReturnsEmpty_ThrowsInvalidOperationException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]>()); // Empty result

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));

        ex.Message.ShouldContain("no embeddings");
    }

    [Fact]
    public async Task SearchAsync_WhenVectorStoreClientThrowsHttpRequestException_ThrowsInvalidOperationException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { queryEmbedding });

        _vectorStoreClientMock
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Qdrant unavailable"));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));

        ex.Message.ShouldContain("External service unavailable");
    }

    #endregion

    #region Helper Methods

    private DenseRetriever CreateRetriever(DenseSettings? customSettings = null)
    {
        var settings = customSettings ?? _denseSettings;
        var options = Options.Create(settings);
        return new DenseRetriever(options, _embeddingServiceMock.Object, _vectorStoreClientMock.Object, _loggerMock.Object);
    }

    #endregion
}
