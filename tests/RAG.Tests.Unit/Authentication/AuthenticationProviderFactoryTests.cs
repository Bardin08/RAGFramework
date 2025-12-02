using Microsoft.Extensions.Logging;
using Moq;
using RAG.Infrastructure.Authentication;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for AuthenticationProviderFactory.
/// </summary>
public class AuthenticationProviderFactoryTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly AuthenticationProviderFactory _factory;

    public AuthenticationProviderFactoryTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        _factory = new AuthenticationProviderFactory(_mockLoggerFactory.Object);
    }

    [Fact]
    public void Create_WithKeycloak_ReturnsKeycloakAuthenticationProvider()
    {
        // Act
        var provider = _factory.Create("keycloak");

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<KeycloakAuthenticationProvider>();
        provider.ProviderName.ShouldBe("keycloak");
    }

    [Theory]
    [InlineData("Keycloak")]
    [InlineData("KEYCLOAK")]
    [InlineData("KeyCloak")]
    public void Create_WithKeycloakCaseInsensitive_ReturnsKeycloakAuthenticationProvider(string providerName)
    {
        // Act
        var provider = _factory.Create(providerName);

        // Assert
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<KeycloakAuthenticationProvider>();
    }

    [Fact]
    public void Create_WithAuth0_ThrowsNotSupportedException()
    {
        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => _factory.Create("auth0"));
        exception.Message.ShouldContain("Auth0");
        exception.Message.ShouldContain("post-MVP");
    }

    [Theory]
    [InlineData("azuread")]
    [InlineData("azure-ad")]
    [InlineData("azure_ad")]
    public void Create_WithAzureAd_ThrowsNotSupportedException(string providerName)
    {
        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => _factory.Create(providerName));
        exception.Message.ShouldContain("Azure AD");
        exception.Message.ShouldContain("post-MVP");
    }

    [Fact]
    public void Create_WithCustom_ThrowsNotSupportedException()
    {
        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => _factory.Create("custom"));
        exception.Message.ShouldContain("Custom");
        exception.Message.ShouldContain("IAuthenticationProvider");
    }

    [Fact]
    public void Create_WithUnknownProvider_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => _factory.Create("unknown-provider"));
        exception.Message.ShouldContain("Unknown authentication provider");
        exception.Message.ShouldContain("unknown-provider");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyProvider_ThrowsArgumentException(string? providerName)
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => _factory.Create(providerName!));
        exception.Message.ShouldContain("cannot be null or empty");
    }

    [Fact]
    public void SupportedProviders_ReturnsKeycloak()
    {
        // Act
        var supported = AuthenticationProviderFactory.SupportedProviders;

        // Assert
        supported.ShouldNotBeNull();
        supported.ShouldContain("keycloak");
        supported.Count.ShouldBe(1);
    }

    [Fact]
    public void PlannedProviders_ReturnsExpectedProviders()
    {
        // Act
        var planned = AuthenticationProviderFactory.PlannedProviders;

        // Assert
        planned.ShouldNotBeNull();
        planned.ShouldContain("auth0");
        planned.ShouldContain("azuread");
        planned.ShouldContain("custom");
        planned.Count.ShouldBe(3);
    }

    [Fact]
    public void Create_CreatesNewInstanceEachTime()
    {
        // Act
        var provider1 = _factory.Create("keycloak");
        var provider2 = _factory.Create("keycloak");

        // Assert
        provider1.ShouldNotBe(provider2);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsException()
    {
        // Act & Assert - ArgumentNullException is thrown when CreateLogger is called
        Should.Throw<ArgumentNullException>(() =>
        {
            var factory = new AuthenticationProviderFactory(null!);
            factory.Create("keycloak");
        });
    }
}
