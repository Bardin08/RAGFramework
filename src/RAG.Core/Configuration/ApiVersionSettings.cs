namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for API versioning.
/// </summary>
public class ApiVersionSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ApiVersioning";

    /// <summary>
    /// Default API version when not specified in request.
    /// </summary>
    public string DefaultVersion { get; set; } = "1.0";

    /// <summary>
    /// List of currently supported API versions.
    /// </summary>
    public List<string> SupportedVersions { get; set; } = ["1.0"];

    /// <summary>
    /// List of deprecated API versions (still functional but not recommended).
    /// </summary>
    public List<string> DeprecatedVersions { get; set; } = [];

    /// <summary>
    /// Whether to assume default version when not specified.
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    /// <summary>
    /// Whether to report supported versions in response headers.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;
}
