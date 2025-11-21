using RAG.Core.Domain;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Domain;

public class DocumentChunkTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesDocumentChunk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var text = "This is a chunk of text";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var startIndex = 0;
        var endIndex = 23;

        // Act
        var chunk = new DocumentChunk(id, documentId, text, embedding, startIndex, endIndex);

        // Assert
        chunk.ShouldNotBeNull();
        chunk.Id.ShouldBe(id);
        chunk.DocumentId.ShouldBe(documentId);
        chunk.Text.ShouldBe(text);
        chunk.Embedding.ShouldBe(embedding);
        chunk.StartIndex.ShouldBe(startIndex);
        chunk.EndIndex.ShouldBe(endIndex);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.Empty, Guid.NewGuid(), "text", new float[] { 0.1f }, 0, 10))
            .Message.ShouldContain("Chunk ID cannot be empty");
    }

    [Fact]
    public void Constructor_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.Empty, "text", new float[] { 0.1f }, 0, 10))
            .Message.ShouldContain("Document ID cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidText_ThrowsArgumentException(string invalidText)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), invalidText, new float[] { 0.1f }, 0, 10))
            .Message.ShouldContain("Chunk text cannot be empty");
    }

    [Fact]
    public void Constructor_WithNullEmbedding_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", null!, 0, 10))
            .Message.ShouldContain("Embedding cannot be null or empty");
    }

    [Fact]
    public void Constructor_WithEmptyEmbedding_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", new float[] { }, 0, 10))
            .Message.ShouldContain("Embedding cannot be null or empty");
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", new float[] { 0.1f }, -1, 10))
            .Message.ShouldContain("StartIndex cannot be negative");
    }

    [Fact]
    public void Constructor_WithEndIndexLessThanStartIndex_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", new float[] { 0.1f }, 10, 5))
            .Message.ShouldContain("EndIndex must be greater than StartIndex");
    }

    [Fact]
    public void Constructor_WithEndIndexEqualToStartIndex_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", new float[] { 0.1f }, 10, 10))
            .Message.ShouldContain("EndIndex must be greater than StartIndex");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f };
        var chunk1 = new DocumentChunk(id, documentId, "text", embedding, 0, 10);
        var chunk2 = new DocumentChunk(id, documentId, "text", embedding, 0, 10);

        // Act & Assert
        chunk1.ShouldBe(chunk2);
    }

    [Fact]
    public void Embedding_WithDifferentDimensions_Works()
    {
        // Arrange
        var embedding384 = new float[384];
        var embedding768 = new float[768];

        // Act
        var chunk384 = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", embedding384, 0, 10);
        var chunk768 = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), "text", embedding768, 0, 10);

        // Assert
        chunk384.Embedding.Length.ShouldBe(384);
        chunk768.Embedding.Length.ShouldBe(768);
    }
}
