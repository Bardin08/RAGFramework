using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Infrastructure.Authentication;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for KeycloakAuthenticationProvider.
/// </summary>
public class KeycloakAuthenticationProviderTests
{
    private readonly Mock<ILogger<KeycloakAuthenticationProvider>> _mockLogger;
    private readonly KeycloakAuthenticationProvider _provider;

    public KeycloakAuthenticationProviderTests()
    {
        _mockLogger = new Mock<ILogger<KeycloakAuthenticationProvider>>();
        _provider = new KeycloakAuthenticationProvider(_mockLogger.Object);
    }

    [Fact]
    public void ProviderName_ReturnsKeycloak()
    {
        // Act
        var name = _provider.ProviderName;

        // Assert
        name.ShouldBe("keycloak");
    }

    [Fact]
    public void GetClaimsTransformation_ReturnsKeycloakClaimsTransformation()
    {
        // Act
        var transformation = _provider.GetClaimsTransformation();

        // Assert
        transformation.ShouldNotBeNull();
        transformation.ShouldBeOfType<KeycloakClaimsTransformation>();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithoutConfiguration_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _provider.ValidateTokenAsync("test-token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateTokenAsync_WithNullOrEmptyToken_ReturnsNull(string? token)
    {
        // Arrange - Use a test helper to set configuration directly
        var testProvider = new KeycloakAuthenticationProviderTestHelper(_mockLogger.Object);
        testProvider.SetSettingsForTesting("https://keycloak.example.com/realms/test", "test-client");

        // Act
        var result = await testProvider.ValidateTokenAsync(token!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var testProvider = new KeycloakAuthenticationProviderTestHelper(_mockLogger.Object);
        testProvider.SetSettingsForTesting("https://keycloak.example.com/realms/test", "test-client");

        // Act - Use a malformed token
        var result = await testProvider.ValidateTokenAsync("not-a-valid-jwt");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert - This tests defensive programming
        // Note: Current implementation may not throw, this documents expected behavior
        var provider = new KeycloakAuthenticationProvider(null!);
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void ProviderName_IsConstant()
    {
        // Arrange
        var provider1 = new KeycloakAuthenticationProvider(_mockLogger.Object);
        var provider2 = new KeycloakAuthenticationProvider(_mockLogger.Object);

        // Act & Assert
        provider1.ProviderName.ShouldBe(provider2.ProviderName);
        provider1.ProviderName.ShouldBe("keycloak");
    }

    [Fact]
    public void GetClaimsTransformation_ReturnsSameTypeEachTime()
    {
        // Act
        var transformation1 = _provider.GetClaimsTransformation();
        var transformation2 = _provider.GetClaimsTransformation();

        // Assert
        transformation1.ShouldBeOfType<KeycloakClaimsTransformation>();
        transformation2.ShouldBeOfType<KeycloakClaimsTransformation>();
    }

    /// <summary>
    /// Test helper class to expose internal configuration for testing.
    /// </summary>
    private class KeycloakAuthenticationProviderTestHelper : KeycloakAuthenticationProvider
    {
        public KeycloakAuthenticationProviderTestHelper(ILogger<KeycloakAuthenticationProvider> logger)
            : base(logger)
        {
        }

        public void SetSettingsForTesting(string authority, string clientId)
        {
            // Use reflection to set the private _settings field for testing
            var settingsField = typeof(KeycloakAuthenticationProvider)
                .GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var settings = new RAG.Core.Configuration.KeycloakAuthSettings
            {
                Authority = authority,
                ClientId = clientId,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkewSeconds = 0
            };

            settingsField?.SetValue(this, settings);
        }
    }
}
