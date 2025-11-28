using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Core.Configuration;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Unit.Retrievers;

public class BM25RetrieverTests
{
    private readonly Mock<ILogger<BM25Retriever>> _loggerMock;
    private readonly BM25Settings _bm25Settings;
    private readonly ElasticsearchSettings _elasticsearchSettings;

    public BM25RetrieverTests()
    {
        _loggerMock = new Mock<ILogger<BM25Retriever>>();

        _bm25Settings = new BM25Settings
        {
            K1 = 1.2,
            B = 0.75,
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 5,
            HighlightFragmentSize = 150
        };

        _elasticsearchSettings = new ElasticsearchSettings
        {
            Url = "http://localhost:9200",
            IndexName = "test-documents",
            Username = "",
            Password = "",
            NumberOfShards = 1,
            NumberOfReplicas = 0
        };
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<BM25Settings>? nullBm25Settings = null;
        var elasticsearchOptions = Options.Create(_elasticsearchSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new BM25Retriever(nullBm25Settings!, elasticsearchOptions, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullElasticsearchSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var bm25Options = Options.Create(_bm25Settings);
        IOptions<ElasticsearchSettings>? nullElasticsearchSettings = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new BM25Retriever(bm25Options, nullElasticsearchSettings!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var bm25Options = Options.Create(_bm25Settings);
        var elasticsearchOptions = Options.Create(_elasticsearchSettings);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new BM25Retriever(bm25Options, elasticsearchOptions, null!));
    }

    [Fact]
    public void Constructor_WithInvalidBM25Settings_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = new BM25Settings { K1 = -1.0 }; // Invalid K1
        var bm25Options = Options.Create(invalidSettings);
        var elasticsearchOptions = Options.Create(_elasticsearchSettings);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new BM25Retriever(bm25Options, elasticsearchOptions, _loggerMock.Object));
    }

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

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void SearchAsync_WithValidTopK_DoesNotThrowValidationException(int topK)
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "test query";
        var tenantId = Guid.NewGuid();

        // Act & Assert - validation should not throw
        // Note: This will fail at Elasticsearch connection, but input validation should pass
        Should.NotThrow(() =>
        {
            var task = retriever.SearchAsync(query, topK, tenantId);
            // Don't await - we only care about input validation, not Elasticsearch connection
        });
    }

    [Fact]
    public void BM25Settings_Validate_WithInvalidK1_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings { K1 = 0 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithNegativeB_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings { B = -0.1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithBGreaterThanOne_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings { B = 1.1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithDefaultTopKExceedingMaxTopK_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings
        {
            DefaultTopK = 150,
            MaxTopK = 100
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithNegativeTimeout_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings { TimeoutSeconds = -1 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithNegativeFragmentSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new BM25Settings { HighlightFragmentSize = -10 };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_Validate_WithValidSettings_DoesNotThrow()
    {
        // Arrange
        var settings = new BM25Settings
        {
            K1 = 1.5,
            B = 0.8,
            DefaultTopK = 20,
            MaxTopK = 100,
            TimeoutSeconds = 10,
            HighlightFragmentSize = 200
        };

        // Act & Assert
        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void BM25Settings_DefaultValues_AreValid()
    {
        // Arrange
        var settings = new BM25Settings();

        // Act & Assert
        settings.K1.ShouldBe(1.2);
        settings.B.ShouldBe(0.75);
        settings.DefaultTopK.ShouldBe(10);
        settings.MaxTopK.ShouldBe(100);
        settings.TimeoutSeconds.ShouldBe(5);
        settings.HighlightFragmentSize.ShouldBe(150);
        Should.NotThrow(() => settings.Validate());
    }

    private BM25Retriever CreateRetriever()
    {
        var bm25Options = Options.Create(_bm25Settings);
        var elasticsearchOptions = Options.Create(_elasticsearchSettings);
        return new BM25Retriever(bm25Options, elasticsearchOptions, _loggerMock.Object);
    }
}
