namespace RAG.Core.Configuration;

/// <summary>
/// CORS configuration settings for frontend integration.
/// Supports environment-specific origin whitelisting with credentials support.
/// </summary>
public class CorsSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Allowed origins for CORS requests.
    /// In development: localhost ports (3000, 5173, 8080).
    /// In production: specific frontend domains.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Allowed HTTP methods for CORS requests.
    /// Defaults configured in appsettings.json: GET, POST, PUT, DELETE, OPTIONS, PATCH.
    /// </summary>
    public string[] AllowedMethods { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Allowed headers in CORS requests.
    /// Defaults configured in appsettings.json: Content-Type, Authorization, X-Requested-With, Accept, Origin.
    /// </summary>
    public string[] AllowedHeaders { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Headers exposed to the client in CORS responses.
    /// Defaults configured in appsettings.json: rate limit headers, request ID, API versioning headers.
    /// </summary>
    public string[] ExposedHeaders { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers) in CORS requests.
    /// When true, AllowedOrigins cannot contain wildcards.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// Maximum time (in seconds) browsers can cache preflight responses.
    /// Default: 600 seconds (10 minutes).
    /// </summary>
    public int MaxAgeSeconds { get; set; } = 600;
}
