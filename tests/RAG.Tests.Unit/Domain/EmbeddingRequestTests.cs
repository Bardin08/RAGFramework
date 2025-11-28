using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class EmbeddingRequestTests
{
    [Fact]
    public void Validate_WithValidTexts_DoesNotThrow()
    {
        // Arrange
        var request = new EmbeddingRequest(new List<string> { "text1", "text2", "text3" });

        // Act & Assert
        Should.NotThrow(() => request.Validate());
    }

    [Fact]
    public void Validate_WithNullTextsList_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new EmbeddingRequest(null!);

        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() => request.Validate());
        exception.ParamName.ShouldBe("Texts");
    }

    [Fact]
    public void Validate_WithEmptyTextsList_ThrowsArgumentException()
    {
        // Arrange
        var request = new EmbeddingRequest(new List<string>());

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => request.Validate());
        exception.ParamName.ShouldBe("Texts");
        exception.Message.ShouldContain("cannot be empty");
    }

    [Fact]
    public void Validate_WithNullTextInList_ThrowsArgumentException()
    {
        // Arrange
        var request = new EmbeddingRequest(new List<string> { "text1", null!, "text3" });

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => request.Validate());
        exception.ParamName.ShouldBe("Texts");
        exception.Message.ShouldContain("index 1");
    }

    [Fact]
    public void Validate_WithEmptyTextInList_ThrowsArgumentException()
    {
        // Arrange
        var request = new EmbeddingRequest(new List<string> { "text1", "", "text3" });

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => request.Validate());
        exception.ParamName.ShouldBe("Texts");
        exception.Message.ShouldContain("index 1");
    }

    [Fact]
    public void Validate_WithWhitespaceTextInList_ThrowsArgumentException()
    {
        // Arrange
        var request = new EmbeddingRequest(new List<string> { "text1", "   ", "text3" });

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => request.Validate());
        exception.ParamName.ShouldBe("Texts");
        exception.Message.ShouldContain("index 1");
    }
}
