using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class DocumentTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesDocument()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = "Test Document";
        var content = "This is test content";
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var source = "https://example.com";
        var metadata = new Dictionary<string, object> { { "author", "Test Author" } };
        var chunkIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var document = new Document(id, title, content, tenantId, ownerId, source, metadata, chunkIds);

        // Assert
        document.ShouldNotBeNull();
        document.Id.ShouldBe(id);
        document.Title.ShouldBe(title);
        document.Content.ShouldBe(content);
        document.TenantId.ShouldBe(tenantId);
        document.OwnerId.ShouldBe(ownerId);
        document.Source.ShouldBe(source);
        document.Metadata.ShouldBe(metadata);
        document.ChunkIds.ShouldBe(chunkIds);
    }

    [Fact]
    public void Constructor_WithoutOptionalParameters_CreatesDocument()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = "Test Document";
        var content = "This is test content";
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        // Act
        var document = new Document(id, title, content, tenantId, ownerId);

        // Assert
        document.ShouldNotBeNull();
        document.Id.ShouldBe(id);
        document.Title.ShouldBe(title);
        document.Content.ShouldBe(content);
        document.TenantId.ShouldBe(tenantId);
        document.OwnerId.ShouldBe(ownerId);
        document.Source.ShouldBeNull();
        document.Metadata.ShouldNotBeNull();
        document.Metadata.ShouldBeEmpty();
        document.ChunkIds.ShouldNotBeNull();
        document.ChunkIds.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Document(Guid.Empty, "title", "content", Guid.NewGuid(), Guid.NewGuid()))
            .Message.ShouldContain("Document ID cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTitle_ThrowsArgumentException(string invalidTitle)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Document(Guid.NewGuid(), invalidTitle, "content", Guid.NewGuid(), Guid.NewGuid()))
            .Message.ShouldContain("Document title cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidContent_ThrowsArgumentException(string invalidContent)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Document(Guid.NewGuid(), "title", invalidContent, Guid.NewGuid(), Guid.NewGuid()))
            .Message.ShouldContain("Document content cannot be empty");
    }

    [Fact]
    public void ChunkIds_CanBeModified()
    {
        // Arrange
        var document = new Document(Guid.NewGuid(), "title", "content", Guid.NewGuid(), Guid.NewGuid());
        var newChunkId = Guid.NewGuid();

        // Act
        document.ChunkIds.Add(newChunkId);

        // Assert
        document.ChunkIds.ShouldContain(newChunkId);
        document.ChunkIds.Count.ShouldBe(1);
    }

    [Fact]
    public void Metadata_CanBeModified()
    {
        // Arrange
        var document = new Document(Guid.NewGuid(), "title", "content", Guid.NewGuid(), Guid.NewGuid());

        // Act
        document.Metadata["key"] = "value";

        // Assert
        document.Metadata.ShouldContainKey("key");
        document.Metadata["key"].ShouldBe("value");
    }
}
