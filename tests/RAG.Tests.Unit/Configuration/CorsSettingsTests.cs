using Microsoft.Extensions.Configuration;
using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for CorsSettings configuration.
/// </summary>
public class CorsSettingsTests
{
    [Fact]
    public void SectionName_ReturnsCors()
    {
        // Assert
        CorsSettings.SectionName.ShouldBe("Cors");
    }

    [Fact]
    public void DefaultValues_AreCorrectlySet()
    {
        // Arrange
        var settings = new CorsSettings();

        // Assert - arrays are empty by default (values come from appsettings.json)
        settings.AllowedOrigins.ShouldBeEmpty();
        settings.AllowedMethods.ShouldBeEmpty();
        settings.AllowedHeaders.ShouldBeEmpty();
        settings.ExposedHeaders.ShouldBeEmpty();
        // Scalar values have defaults
        settings.AllowCredentials.ShouldBeTrue();
        settings.MaxAgeSeconds.ShouldBe(600);
    }

    [Fact]
    public void CanBindFromConfiguration_AllSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:AllowedOrigins:0", "https://example.com" },
                { "Cors:AllowedOrigins:1", "https://app.example.com" },
                { "Cors:AllowedMethods:0", "GET" },
                { "Cors:AllowedMethods:1", "POST" },
                { "Cors:AllowedHeaders:0", "Content-Type" },
                { "Cors:AllowedHeaders:1", "Authorization" },
                { "Cors:ExposedHeaders:0", "X-Custom-Header" },
                { "Cors:AllowCredentials", "true" },
                { "Cors:MaxAgeSeconds", "3600" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.AllowedOrigins.ShouldBe(new[] { "https://example.com", "https://app.example.com" });
        settings.AllowedMethods.ShouldBe(new[] { "GET", "POST" });
        settings.AllowedHeaders.ShouldBe(new[] { "Content-Type", "Authorization" });
        settings.ExposedHeaders.ShouldBe(new[] { "X-Custom-Header" });
        settings.AllowCredentials.ShouldBeTrue();
        settings.MaxAgeSeconds.ShouldBe(3600);
    }

    [Fact]
    public void CanBindFromConfiguration_DevelopmentOrigins()
    {
        // Arrange - typical development configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:AllowedOrigins:0", "http://localhost:3000" },
                { "Cors:AllowedOrigins:1", "http://localhost:5173" },
                { "Cors:AllowedOrigins:2", "http://localhost:8080" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.AllowedOrigins.Length.ShouldBe(3);
        settings.AllowedOrigins.ShouldContain("http://localhost:3000");
        settings.AllowedOrigins.ShouldContain("http://localhost:5173");
        settings.AllowedOrigins.ShouldContain("http://localhost:8080");
    }

    [Fact]
    public void CanBindFromConfiguration_ProductionOrigins()
    {
        // Arrange - typical production configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:AllowedOrigins:0", "https://rag.example.com" },
                { "Cors:AllowedOrigins:1", "https://admin.rag.example.com" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.AllowedOrigins.Length.ShouldBe(2);
        settings.AllowedOrigins.ShouldContain("https://rag.example.com");
        settings.AllowedOrigins.ShouldContain("https://admin.rag.example.com");
    }

    [Fact]
    public void CanBindFromConfiguration_CredentialsDisabled()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:AllowCredentials", "false" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.AllowCredentials.ShouldBeFalse();
    }

    [Fact]
    public void PartialConfiguration_UsesDefaultsForMissingValues()
    {
        // Arrange - only origins specified, arrays remain empty, scalars use defaults
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:AllowedOrigins:0", "https://myapp.com" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.AllowedOrigins.ShouldBe(new[] { "https://myapp.com" });
        // Arrays not specified remain empty (defaults come from appsettings.json in real config)
        settings.AllowedMethods.ShouldBeEmpty();
        // Scalar defaults still apply
        settings.AllowCredentials.ShouldBeTrue();
        settings.MaxAgeSeconds.ShouldBe(600);
    }

    [Fact]
    public void EmptyConfiguration_ReturnsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldBeNull();
    }

    [Fact]
    public void ExposedHeaders_WhenConfigured_ContainsRateLimitHeaders()
    {
        // Arrange - simulating appsettings.json configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:ExposedHeaders:0", "X-RateLimit-Limit" },
                { "Cors:ExposedHeaders:1", "X-RateLimit-Remaining" },
                { "Cors:ExposedHeaders:2", "X-RateLimit-Reset" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.ExposedHeaders.ShouldContain("X-RateLimit-Limit");
        settings.ExposedHeaders.ShouldContain("X-RateLimit-Remaining");
        settings.ExposedHeaders.ShouldContain("X-RateLimit-Reset");
    }

    [Fact]
    public void ExposedHeaders_WhenConfigured_ContainsApiVersioningHeaders()
    {
        // Arrange - simulating appsettings.json configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:ExposedHeaders:0", "api-supported-versions" },
                { "Cors:ExposedHeaders:1", "api-deprecated-versions" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.ExposedHeaders.ShouldContain("api-supported-versions");
        settings.ExposedHeaders.ShouldContain("api-deprecated-versions");
    }

    [Fact]
    public void ExposedHeaders_WhenConfigured_ContainsRequestIdHeader()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cors:ExposedHeaders:0", "X-Request-Id" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.ExposedHeaders.ShouldContain("X-Request-Id");
    }

    [Fact]
    public void MaxAgeSeconds_DefaultsTo600()
    {
        // Arrange
        var settings = new CorsSettings();

        // Assert - 10 minutes preflight cache
        settings.MaxAgeSeconds.ShouldBe(600);
    }
}
