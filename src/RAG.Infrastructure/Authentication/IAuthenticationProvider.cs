using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace RAG.Infrastructure.Authentication;

/// <summary>
/// Interface for authentication provider implementations.
/// Supports multiple identity providers (Keycloak, Auth0, Azure AD, etc.).
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Gets the name of this authentication provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Configures authentication services using the authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder to configure.</param>
    /// <param name="configuration">The configuration containing provider-specific settings.</param>
    void ConfigureAuthentication(AuthenticationBuilder builder, IConfiguration configuration);

    /// <summary>
    /// Validates a JWT token and returns the claims principal if valid.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The claims principal if the token is valid; null if validation fails.
    /// </returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the claims transformation for this provider, if any.
    /// </summary>
    /// <returns>The claims transformation instance, or null if no transformation is needed.</returns>
    IClaimsTransformation? GetClaimsTransformation();
}
