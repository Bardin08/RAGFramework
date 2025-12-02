using System.ComponentModel.DataAnnotations;

namespace RAG.Core.Configuration;

/// <summary>
/// Root authentication configuration settings supporting multiple providers.
/// </summary>
public class AuthenticationSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// Gets or sets the authentication provider to use.
    /// Supported values: "keycloak", "auth0", "azuread", "custom".
    /// </summary>
    [Required]
    public string Provider { get; set; } = "keycloak";

    /// <summary>
    /// Gets or sets Keycloak-specific authentication settings.
    /// </summary>
    public KeycloakAuthSettings Keycloak { get; set; } = new();

    /// <summary>
    /// Gets or sets Auth0-specific authentication settings (placeholder for future).
    /// </summary>
    public Auth0AuthSettings Auth0 { get; set; } = new();

    /// <summary>
    /// Gets or sets Azure AD-specific authentication settings (placeholder for future).
    /// </summary>
    public AzureAdAuthSettings AzureAd { get; set; } = new();
}

/// <summary>
/// Keycloak OpenID Connect authentication settings.
/// </summary>
public class KeycloakAuthSettings
{
    /// <summary>
    /// Gets or sets the Keycloak authority URL (realm endpoint).
    /// Example: "http://keycloak:8080/realms/rag-system"
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID registered in Keycloak.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for confidential clients.
    /// Should be stored in User Secrets or environment variables in production.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets whether HTTPS is required for metadata endpoints.
    /// Set to false only in development environments.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected audience for token validation.
    /// Typically matches the ClientId.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the clock skew tolerance for token lifetime validation.
    /// Zero means strict validation. Default is 0 seconds.
    /// </summary>
    public int ClockSkewSeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to validate the token issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate token lifetime.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the issuer signing key.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;
}

/// <summary>
/// Auth0 authentication settings (placeholder for future implementation).
/// </summary>
public class Auth0AuthSettings
{
    /// <summary>
    /// Gets or sets the Auth0 domain.
    /// Example: "your-tenant.auth0.com"
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Auth0 client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Auth0 client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the API audience identifier.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}

/// <summary>
/// Azure Active Directory authentication settings (placeholder for future implementation).
/// </summary>
public class AzureAdAuthSettings
{
    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD client (application) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD instance URL.
    /// Default: "https://login.microsoftonline.com/"
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Gets or sets the API audience identifier.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}
