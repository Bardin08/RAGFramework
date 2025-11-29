using RAG.Application.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for ApproximateTokenCounter.
/// Tests AC#3: Token counting with character count / 4 approximation.
/// </summary>
public class ApproximateTokenCounterTests
{
    private readonly ApproximateTokenCounter _counter;

    public ApproximateTokenCounterTests()
    {
        _counter = new ApproximateTokenCounter();
    }

    [Fact]
    public void CountTokens_WithNullText_ReturnsZero()
    {
        // Arrange
        string? nullText = null;

        // Act
        var count = _counter.CountTokens(nullText!);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public void CountTokens_WithEmptyString_ReturnsZero()
    {
        // Arrange
        var emptyText = "";

        // Act
        var count = _counter.CountTokens(emptyText);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public void CountTokens_WithFourCharacters_ReturnsOne()
    {
        // Arrange - AC#3: 1 token ≈ 4 characters
        var text = "test"; // Exactly 4 characters

        // Act
        var count = _counter.CountTokens(text);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public void CountTokens_WithEightCharacters_ReturnsTwo()
    {
        // Arrange - AC#3: 8 characters / 4 = 2 tokens
        var text = "testing!"; // Exactly 8 characters

        // Act
        var count = _counter.CountTokens(text);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public void CountTokens_WithFiveCharacters_RoundsUpToTwo()
    {
        // Arrange - AC#3: 5 characters / 4 = 1.25, should ceil to 2
        var text = "hello"; // 5 characters

        // Act
        var count = _counter.CountTokens(text);

        // Assert
        count.ShouldBe(2); // Ceiling of 1.25
    }

    [Fact]
    public void CountTokens_WithLongText_CalculatesCorrectly()
    {
        // Arrange - AC#3, AC#7: Test with realistic text
        var text = "Machine learning is a subset of artificial intelligence that focuses on algorithms."; // 84 characters

        // Act
        var count = _counter.CountTokens(text);

        // Assert
        // 84 / 4 = 21 tokens
        count.ShouldBe(21);
    }

    [Fact]
    public void CountTokens_WithWhitespace_CountsAllCharacters()
    {
        // Arrange
        var text = "a   b"; // 5 characters including spaces

        // Act
        var count = _counter.CountTokens(text);

        // Assert
        count.ShouldBe(2); // Ceiling of 5/4 = 1.25
    }

    [Theory]
    [InlineData("a", 1)]
    [InlineData("ab", 1)]
    [InlineData("abc", 1)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    [InlineData("abcdefgh", 2)]
    [InlineData("abcdefghi", 3)]
    public void CountTokens_WithVariousLengths_ReturnsExpectedCount(string text, int expectedTokens)
    {
        // Act
        var count = _counter.CountTokens(text);

        // Assert
        count.ShouldBe(expectedTokens);
    }
}
