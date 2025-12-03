namespace RAG.Core.Configuration;

/// <summary>
/// Rate limiting configuration settings.
/// Supports IP-based and client-based rate limiting with tiered limits.
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Enable or disable rate limiting globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enable endpoint-specific rate limiting.
    /// When true, different endpoints can have different limits.
    /// </summary>
    public bool EnableEndpointRateLimiting { get; set; } = true;

    /// <summary>
    /// Stack blocked requests. When true, rejected requests count toward the limit.
    /// </summary>
    public bool StackBlockedRequests { get; set; } = false;

    /// <summary>
    /// Header name for real IP address when behind a proxy/load balancer.
    /// </summary>
    public string RealIpHeader { get; set; } = "X-Real-IP";

    /// <summary>
    /// Header name for client identification.
    /// </summary>
    public string ClientIdHeader { get; set; } = "X-ClientId";

    /// <summary>
    /// HTTP status code to return when rate limit is exceeded.
    /// </summary>
    public int HttpStatusCode { get; set; } = 429;

    /// <summary>
    /// IP addresses to whitelist from rate limiting.
    /// </summary>
    public List<string> IpWhitelist { get; set; } = new() { "127.0.0.1", "::1" };

    /// <summary>
    /// Client IDs to whitelist from rate limiting.
    /// </summary>
    public List<string> ClientWhitelist { get; set; } = new();

    /// <summary>
    /// Default rate limit tier settings.
    /// </summary>
    public RateLimitTiers Tiers { get; set; } = new();

    /// <summary>
    /// Endpoint-specific rate limit rules.
    /// </summary>
    public List<EndpointRateLimitRule> EndpointRules { get; set; } = new();

    /// <summary>
    /// General rate limit rules applied to all endpoints.
    /// </summary>
    public List<GeneralRateLimitRule> GeneralRules { get; set; } = new();
}

/// <summary>
/// Rate limit tiers for different user types.
/// </summary>
public class RateLimitTiers
{
    /// <summary>
    /// Rate limit for anonymous/unauthenticated users (requests per minute).
    /// Default: 100 requests/minute per IP.
    /// </summary>
    public int Anonymous { get; set; } = 100;

    /// <summary>
    /// Rate limit for authenticated users (requests per minute).
    /// Default: 200 requests/minute.
    /// </summary>
    public int Authenticated { get; set; } = 200;

    /// <summary>
    /// Rate limit for admin users (requests per minute).
    /// Default: 500 requests/minute.
    /// </summary>
    public int Admin { get; set; } = 500;
}

/// <summary>
/// Endpoint-specific rate limit rule.
/// </summary>
public class EndpointRateLimitRule
{
    /// <summary>
    /// Endpoint pattern (e.g., "*", "post:/api/query", "get:/api/documents").
    /// Format: "{method}:{path}" or "*" for all endpoints.
    /// </summary>
    public string Endpoint { get; set; } = "*";

    /// <summary>
    /// Time period for the rate limit (e.g., "1m", "1h", "1d").
    /// </summary>
    public string Period { get; set; } = "1m";

    /// <summary>
    /// Maximum number of requests allowed in the period.
    /// </summary>
    public int Limit { get; set; }
}

/// <summary>
/// General rate limit rule applied to all endpoints.
/// </summary>
public class GeneralRateLimitRule
{
    /// <summary>
    /// Endpoint pattern.
    /// </summary>
    public string Endpoint { get; set; } = "*";

    /// <summary>
    /// Time period for the rate limit.
    /// </summary>
    public string Period { get; set; } = "1m";

    /// <summary>
    /// Maximum requests allowed in the period.
    /// </summary>
    public int Limit { get; set; }
}
