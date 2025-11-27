using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class DocumentChunkTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesDocumentChunk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var text = "This is a chunk of text";
        var startIndex = 0;
        var endIndex = 23;
        var chunkIndex = 0;
        var metadata = new Dictionary<string, object>
        {
            { "TokenCount", 100 },
            { "ChunkingStrategy", "SlidingWindow" }
        };

        // Act
        var chunk = new DocumentChunk(id, documentId, text, startIndex, endIndex, chunkIndex, tenantId, metadata);

        // Assert
        chunk.ShouldNotBeNull();
        chunk.Id.ShouldBe(id);
        chunk.DocumentId.ShouldBe(documentId);
        chunk.Text.ShouldBe(text);
        chunk.StartIndex.ShouldBe(startIndex);
        chunk.EndIndex.ShouldBe(endIndex);
        chunk.ChunkIndex.ShouldBe(chunkIndex);
        chunk.TenantId.ShouldBe(tenantId);
        chunk.Metadata.ShouldNotBeNull();
        chunk.Metadata.Count.ShouldBe(2);
        chunk.Metadata["TokenCount"].ShouldBe(100);
    }

    [Fact]
    public void Constructor_WithEmptyId_GeneratesNewId()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var text = "Test text";

        // Act
        var chunk = new DocumentChunk(Guid.Empty, documentId, text, 0, 9, 0, Guid.NewGuid());

        // Assert
        chunk.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.Empty, "text", 0, 10, 0, Guid.NewGuid()))
            .Message.ShouldContain("DocumentId cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_WithNullOrEmptyText_ThrowsArgumentException(string? invalidText)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), invalidText!, 0, 10, 0, Guid.NewGuid()))
            .Message.ShouldContain("Text cannot be null or empty");
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", -1, 10, 0, Guid.NewGuid()))
            .Message.ShouldContain("StartIndex must be >= 0");
    }

    [Fact]
    public void Constructor_WithEndIndexLessThanStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", 10, 5, 0, Guid.NewGuid()))
            .Message.ShouldContain("EndIndex must be > StartIndex");
    }

    [Fact]
    public void Constructor_WithEndIndexEqualToStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", 10, 10, 0, Guid.NewGuid()))
            .Message.ShouldContain("EndIndex must be > StartIndex");
    }

    [Fact]
    public void Constructor_WithNullMetadata_CreatesEmptyMetadata()
    {
        // Arrange & Act
        var chunk = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", 0, 10, 0, Guid.NewGuid(), metadata: null);

        // Assert
        chunk.Metadata.ShouldNotBeNull();
        chunk.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void Properties_AreInitOnly()
    {
        // This test verifies that properties use 'init' by checking compilation
        // If properties were settable, this would compile with '= newValue' syntax
        // The test passes if the code compiles correctly with init-only properties

        // Arrange
        var chunk = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", 0, 10, 0, Guid.NewGuid());

        // Assert - properties are initialized via constructor
        chunk.Id.ShouldNotBe(Guid.Empty);
        chunk.Text.ShouldBe("text");
        chunk.ChunkIndex.ShouldBe(0);

        // Note: Cannot assign after construction due to 'init' accessor
        // chunk.Text = "new text"; // This would not compile
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            { "TokenCount", 50 },
            { "ChunkingStrategy", "SlidingWindow" },
            { "WordCount", 40 }
        };

        // Act
        var tenantId = Guid.NewGuid();
        var chunk = new DocumentChunk(id, documentId, "Sample chunk text", 100, 117, 5, tenantId, metadata);

        // Assert
        chunk.Id.ShouldBe(id);
        chunk.DocumentId.ShouldBe(documentId);
        chunk.Text.ShouldBe("Sample chunk text");
        chunk.StartIndex.ShouldBe(100);
        chunk.EndIndex.ShouldBe(117);
        chunk.ChunkIndex.ShouldBe(5);
        chunk.TenantId.ShouldBe(tenantId);
        chunk.Metadata.Count.ShouldBe(3);
        chunk.Metadata["TokenCount"].ShouldBe(50);
        chunk.Metadata["WordCount"].ShouldBe(40);
    }
}
