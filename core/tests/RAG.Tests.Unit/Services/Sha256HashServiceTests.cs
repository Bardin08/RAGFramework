using System.Text;
using RAG.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for SHA-256 hash service.
/// </summary>
public class Sha256HashServiceTests
{
    private readonly Sha256HashService _hashService;

    public Sha256HashServiceTests()
    {
        _hashService = new Sha256HashService();
    }

    [Fact]
    public void ComputeHash_WithKnownInput_ReturnsExpectedHash()
    {
        // Arrange
        var input = "hello world";
        var expectedHash = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        // Act
        var actualHash = _hashService.ComputeHash(stream);

        // Assert
        actualHash.ShouldBe(expectedHash);
    }

    [Fact]
    public void ComputeHash_WithEmptyStream_ReturnsEmptyStringHash()
    {
        // Arrange
        var expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        using var stream = new MemoryStream();

        // Act
        var actualHash = _hashService.ComputeHash(stream);

        // Assert
        actualHash.ShouldBe(expectedHash);
    }

    [Fact]
    public void ComputeHash_WithSameContent_ReturnsSameHash()
    {
        // Arrange
        var content = "test content";
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(content));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var hash1 = _hashService.ComputeHash(stream1);
        var hash2 = _hashService.ComputeHash(stream2);

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputeHash_WithDifferentContent_ReturnsDifferentHashes()
    {
        // Arrange
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("content1"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("content2"));

        // Act
        var hash1 = _hashService.ComputeHash(stream1);
        var hash2 = _hashService.ComputeHash(stream2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ResetsStreamPosition()
    {
        // Arrange
        var content = "test";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        stream.Position = 0;

        // Act
        _hashService.ComputeHash(stream);

        // Assert
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void ComputeHash_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _hashService.ComputeHash(null!));
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHexString()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        // Act
        var hash = _hashService.ComputeHash(stream);

        // Assert
        hash.ShouldBe(hash.ToLowerInvariant());
        hash.Length.ShouldBe(64); // SHA-256 produces 64 hex characters
    }
}
