using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.Configuration;

public class RetrievalSettingsTests
{
    [Fact]
    public void Validate_ValidDefaultStrategy_DoesNotThrow()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "Dense",
            EnableStrategyFallback = true,
            FallbackStrategy = "BM25"
        };

        // Act & Assert
        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData("BM25")]
    [InlineData("Dense")]
    [InlineData("Hybrid")]
    [InlineData("bm25")] // Case insensitive
    [InlineData("dense")] // Case insensitive
    [InlineData("hybrid")] // Case insensitive
    public void Validate_ValidStrategyNames_DoesNotThrow(string strategyName)
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = strategyName,
            EnableStrategyFallback = false
        };

        // Act & Assert
        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("RRF")]
    public void Validate_InvalidDefaultStrategy_ThrowsArgumentException(string invalidStrategy)
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = invalidStrategy,
            EnableStrategyFallback = false
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Invalid DefaultStrategy");
    }

    [Fact]
    public void Validate_EmptyDefaultStrategy_ThrowsArgumentException()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "",
            EnableStrategyFallback = false
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("DefaultStrategy cannot be null or empty");
    }

    [Fact]
    public void Validate_NullDefaultStrategy_ThrowsArgumentException()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = null!,
            EnableStrategyFallback = false
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("DefaultStrategy cannot be null");
    }

    [Theory]
    [InlineData("Invalid")]
    public void Validate_InvalidFallbackStrategy_ThrowsArgumentException(string invalidStrategy)
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "Dense",
            EnableStrategyFallback = true,
            FallbackStrategy = invalidStrategy
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Invalid FallbackStrategy");
    }

    [Fact]
    public void Validate_EmptyFallbackStrategy_ThrowsArgumentException()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "Dense",
            EnableStrategyFallback = true,
            FallbackStrategy = ""
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("FallbackStrategy cannot be null or empty");
    }

    [Fact]
    public void Validate_FallbackEnabledWithNullFallbackStrategy_ThrowsArgumentException()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "Dense",
            EnableStrategyFallback = true,
            FallbackStrategy = null!
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("FallbackStrategy cannot be null");
    }

    [Fact]
    public void Validate_SameFallbackAndDefaultStrategy_ThrowsArgumentException()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "Dense",
            EnableStrategyFallback = true,
            FallbackStrategy = "Dense"
        };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("FallbackStrategy must be different from DefaultStrategy");
    }

    [Fact]
    public void Validate_FallbackDisabled_DoesNotValidateFallbackStrategy()
    {
        // Arrange
        var settings = new RetrievalSettings
        {
            DefaultStrategy = "BM25",
            EnableStrategyFallback = false,
            FallbackStrategy = "Invalid" // Should not be validated when fallback disabled
        };

        // Act & Assert
        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new RetrievalSettings();

        // Assert
        settings.DefaultStrategy.ShouldBe("Dense");
        settings.EnableStrategyFallback.ShouldBeTrue();
        settings.FallbackStrategy.ShouldBe("BM25");
    }
}
