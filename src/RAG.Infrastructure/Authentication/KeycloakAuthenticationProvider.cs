using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RAG.Core.Configuration;

namespace RAG.Infrastructure.Authentication;

/// <summary>
/// Keycloak OpenID Connect authentication provider implementation.
/// Configures JWT Bearer authentication with Keycloak-specific settings.
/// </summary>
public class KeycloakAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<KeycloakAuthenticationProvider> _logger;
    private readonly KeycloakClaimsTransformation _claimsTransformation;
    private KeycloakAuthSettings? _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public KeycloakAuthenticationProvider(ILogger<KeycloakAuthenticationProvider> logger)
    {
        _logger = logger;
        _claimsTransformation = new KeycloakClaimsTransformation();
    }

    /// <inheritdoc />
    public string ProviderName => "keycloak";

    /// <inheritdoc />
    public void ConfigureAuthentication(AuthenticationBuilder builder, IConfiguration configuration)
    {
        var authSettings = configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>();
        _settings = authSettings?.Keycloak ?? throw new InvalidOperationException("Keycloak authentication settings not found in configuration.");

        if (string.IsNullOrWhiteSpace(_settings.Authority))
        {
            throw new InvalidOperationException("Keycloak Authority URL is required.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            throw new InvalidOperationException("Keycloak ClientId is required.");
        }

        _logger.LogInformation("Configuring Keycloak authentication provider with Authority: {Authority}", _settings.Authority);

        // Clear default claim type mapping to preserve original JWT claim names (sub, preferred_username, etc.)
        // Without this, 'sub' gets mapped to ClaimTypes.NameIdentifier and 'sub' is not directly accessible
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = _settings.Authority;
            options.Audience = _settings.Audience ?? _settings.ClientId;
            options.RequireHttpsMetadata = _settings.RequireHttpsMetadata;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = _settings.ValidateIssuer,
                ValidateAudience = _settings.ValidateAudience,
                ValidateLifetime = _settings.ValidateLifetime,
                ValidateIssuerSigningKey = _settings.ValidateIssuerSigningKey,
                ClockSkew = TimeSpan.FromSeconds(_settings.ClockSkewSeconds),
                NameClaimType = "preferred_username",
                RoleClaimType = ClaimTypes.Role
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    _logger.LogWarning(
                        "Authentication failed: {Error}. Exception: {ExceptionType}",
                        context.Exception.Message,
                        context.Exception.GetType().Name);

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var userId = context.Principal?.FindFirst("sub")?.Value;
                    var username = context.Principal?.FindFirst("preferred_username")?.Value;

                    _logger.LogDebug(
                        "Token validated for user {UserId} ({Username})",
                        userId,
                        username);

                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    if (!context.Response.HasStarted)
                    {
                        _logger.LogDebug(
                            "Authentication challenge issued. Error: {Error}, ErrorDescription: {ErrorDescription}",
                            context.Error,
                            context.ErrorDescription);
                    }

                    return Task.CompletedTask;
                }
            };
        });

        _logger.LogInformation("Keycloak authentication provider configured successfully");
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (_settings == null)
        {
            throw new InvalidOperationException("Authentication provider has not been configured. Call ConfigureAuthentication first.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token validation failed: token is null or empty");
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();

            // For standalone token validation, we need to fetch the signing keys from the OIDC discovery document
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_settings.Authority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var openIdConfig = await configurationManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = _settings.ValidateIssuer,
                ValidIssuer = _settings.Authority,
                ValidateAudience = _settings.ValidateAudience,
                ValidAudience = _settings.Audience ?? _settings.ClientId,
                ValidateLifetime = _settings.ValidateLifetime,
                ValidateIssuerSigningKey = _settings.ValidateIssuerSigningKey,
                IssuerSigningKeys = openIdConfig.SigningKeys,
                ClockSkew = TimeSpan.FromSeconds(_settings.ClockSkewSeconds)
            };

            var result = await handler.ValidateTokenAsync(token, validationParameters);

            if (result.IsValid)
            {
                _logger.LogDebug("Token validation succeeded for subject: {Subject}", result.ClaimsIdentity.FindFirst("sub")?.Value);
                return new ClaimsPrincipal(result.ClaimsIdentity);
            }

            _logger.LogWarning("Token validation failed: {Exception}", result.Exception?.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Security token validation failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return null;
        }
    }

    /// <inheritdoc />
    public IClaimsTransformation? GetClaimsTransformation()
    {
        return _claimsTransformation;
    }
}
