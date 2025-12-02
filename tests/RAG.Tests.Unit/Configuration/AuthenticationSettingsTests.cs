using Microsoft.Extensions.Configuration;
using RAG.Core.Configuration;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for AuthenticationSettings configuration.
/// </summary>
public class AuthenticationSettingsTests
{
    [Fact]
    public void SectionName_ReturnsAuthentication()
    {
        // Assert
        AuthenticationSettings.SectionName.ShouldBe("Authentication");
    }

    [Fact]
    public void DefaultValues_AreCorrectlySet()
    {
        // Arrange
        var settings = new AuthenticationSettings();

        // Assert
        settings.Provider.ShouldBe("keycloak");
        settings.Keycloak.ShouldNotBeNull();
        settings.Auth0.ShouldNotBeNull();
        settings.AzureAd.ShouldNotBeNull();
    }

    [Fact]
    public void KeycloakAuthSettings_DefaultValues_AreCorrectlySet()
    {
        // Arrange
        var settings = new KeycloakAuthSettings();

        // Assert
        settings.Authority.ShouldBe(string.Empty);
        settings.ClientId.ShouldBe(string.Empty);
        settings.ClientSecret.ShouldBeNull();
        settings.Audience.ShouldBeNull();
        settings.RequireHttpsMetadata.ShouldBeTrue();
        settings.ClockSkewSeconds.ShouldBe(0);
        settings.ValidateIssuer.ShouldBeTrue();
        settings.ValidateAudience.ShouldBeTrue();
        settings.ValidateLifetime.ShouldBeTrue();
        settings.ValidateIssuerSigningKey.ShouldBeTrue();
    }

    [Fact]
    public void Auth0AuthSettings_DefaultValues_AreCorrectlySet()
    {
        // Arrange
        var settings = new Auth0AuthSettings();

        // Assert
        settings.Domain.ShouldBe(string.Empty);
        settings.ClientId.ShouldBe(string.Empty);
        settings.Audience.ShouldBe(string.Empty);
    }

    [Fact]
    public void AzureAdAuthSettings_DefaultValues_AreCorrectlySet()
    {
        // Arrange
        var settings = new AzureAdAuthSettings();

        // Assert
        settings.TenantId.ShouldBe(string.Empty);
        settings.ClientId.ShouldBe(string.Empty);
        settings.Instance.ShouldBe("https://login.microsoftonline.com/");
    }

    [Fact]
    public void CanBindFromConfiguration_KeycloakSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Provider", "keycloak" },
                { "Authentication:Keycloak:Authority", "https://keycloak.example.com/realms/myrealm" },
                { "Authentication:Keycloak:ClientId", "my-client" },
                { "Authentication:Keycloak:ClientSecret", "secret123" },
                { "Authentication:Keycloak:Audience", "my-api" },
                { "Authentication:Keycloak:RequireHttpsMetadata", "false" },
                { "Authentication:Keycloak:ClockSkewSeconds", "30" },
                { "Authentication:Keycloak:ValidateIssuer", "true" },
                { "Authentication:Keycloak:ValidateAudience", "false" },
                { "Authentication:Keycloak:ValidateLifetime", "true" },
                { "Authentication:Keycloak:ValidateIssuerSigningKey", "true" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.Provider.ShouldBe("keycloak");
        settings.Keycloak.Authority.ShouldBe("https://keycloak.example.com/realms/myrealm");
        settings.Keycloak.ClientId.ShouldBe("my-client");
        settings.Keycloak.ClientSecret.ShouldBe("secret123");
        settings.Keycloak.Audience.ShouldBe("my-api");
        settings.Keycloak.RequireHttpsMetadata.ShouldBeFalse();
        settings.Keycloak.ClockSkewSeconds.ShouldBe(30);
        settings.Keycloak.ValidateIssuer.ShouldBeTrue();
        settings.Keycloak.ValidateAudience.ShouldBeFalse();
    }

    [Fact]
    public void CanBindFromConfiguration_Auth0Settings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Provider", "auth0" },
                { "Authentication:Auth0:Domain", "myapp.auth0.com" },
                { "Authentication:Auth0:ClientId", "auth0-client-id" },
                { "Authentication:Auth0:Audience", "https://api.myapp.com" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.Provider.ShouldBe("auth0");
        settings.Auth0.Domain.ShouldBe("myapp.auth0.com");
        settings.Auth0.ClientId.ShouldBe("auth0-client-id");
        settings.Auth0.Audience.ShouldBe("https://api.myapp.com");
    }

    [Fact]
    public void CanBindFromConfiguration_AzureAdSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Provider", "azuread" },
                { "Authentication:AzureAd:TenantId", "my-tenant-id" },
                { "Authentication:AzureAd:ClientId", "azure-client-id" },
                { "Authentication:AzureAd:Instance", "https://login.microsoftonline.com/" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.Provider.ShouldBe("azuread");
        settings.AzureAd.TenantId.ShouldBe("my-tenant-id");
        settings.AzureAd.ClientId.ShouldBe("azure-client-id");
        settings.AzureAd.Instance.ShouldBe("https://login.microsoftonline.com/");
    }

    [Fact]
    public void PartialConfiguration_UsesDefaultsForMissingValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Keycloak:Authority", "https://keycloak.example.com/realms/test" },
                { "Authentication:Keycloak:ClientId", "test-client" }
            })
            .Build();

        // Act
        var settings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();

        // Assert
        settings.ShouldNotBeNull();
        settings.Provider.ShouldBe("keycloak"); // Default
        settings.Keycloak.Authority.ShouldBe("https://keycloak.example.com/realms/test");
        settings.Keycloak.ClientId.ShouldBe("test-client");
        settings.Keycloak.RequireHttpsMetadata.ShouldBeTrue(); // Default
        settings.Keycloak.ClockSkewSeconds.ShouldBe(0); // Default
    }

    [Fact]
    public void EmptyConfiguration_ReturnsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var settings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();

        // Assert
        settings.ShouldBeNull();
    }
}
