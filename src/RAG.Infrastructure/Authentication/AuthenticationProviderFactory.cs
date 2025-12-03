using Microsoft.Extensions.Logging;

namespace RAG.Infrastructure.Authentication;

/// <summary>
/// Factory for creating authentication provider instances based on configuration.
/// Supports Keycloak, Auth0, Azure AD, and custom providers.
/// </summary>
public class AuthenticationProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationProviderFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating provider-specific loggers.</param>
    public AuthenticationProviderFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates an authentication provider based on the provider name.
    /// </summary>
    /// <param name="providerName">The name of the authentication provider.</param>
    /// <returns>The authentication provider instance.</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider is not supported.</exception>
    /// <exception cref="ArgumentException">Thrown when the provider name is unknown.</exception>
    public IAuthenticationProvider Create(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
        }

        return providerName.ToLowerInvariant() switch
        {
            "keycloak" => new KeycloakAuthenticationProvider(
                _loggerFactory.CreateLogger<KeycloakAuthenticationProvider>()),

            "auth0" => throw new NotSupportedException(
                "Auth0 authentication provider is planned for post-MVP release. " +
                "Please use Keycloak for current authentication needs."),

            "azuread" or "azure-ad" or "azure_ad" => throw new NotSupportedException(
                "Azure AD authentication provider is planned for post-MVP release. " +
                "Please use Keycloak for current authentication needs."),

            "custom" => throw new NotSupportedException(
                "Custom authentication provider requires manual implementation. " +
                "Implement IAuthenticationProvider and register it in the DI container."),

            _ => throw new ArgumentException(
                $"Unknown authentication provider: '{providerName}'. " +
                $"Supported providers: keycloak, auth0 (planned), azuread (planned), custom.",
                nameof(providerName))
        };
    }

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static IReadOnlyList<string> SupportedProviders => new[]
    {
        "keycloak"
    };

    /// <summary>
    /// Gets the list of planned provider names (not yet implemented).
    /// </summary>
    public static IReadOnlyList<string> PlannedProviders => new[]
    {
        "auth0",
        "azuread",
        "custom"
    };
}
