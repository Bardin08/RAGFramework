namespace RAG.Core.Configuration;

/// <summary>
/// Configuration settings for API request validation.
/// </summary>
public class ValidationSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Validation";

    /// <summary>
    /// Maximum allowed query length in characters. Default: 5000.
    /// </summary>
    public int MaxQueryLength { get; set; } = 5000;

    /// <summary>
    /// Maximum allowed TopK value for retrieval results. Default: 100.
    /// </summary>
    public int MaxTopK { get; set; } = 100;

    /// <summary>
    /// Maximum allowed file size in megabytes. Default: 50.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 50;

    /// <summary>
    /// Maximum file size in bytes (computed from MaxFileSizeMB).
    /// </summary>
    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;

    /// <summary>
    /// Allowed file extensions for document upload. Default: .txt, .pdf, .docx.
    /// </summary>
    public string[] AllowedFileExtensions { get; set; } = [".txt", ".pdf", ".docx"];

    /// <summary>
    /// Maximum allowed document title length. Default: 500.
    /// </summary>
    public int MaxTitleLength { get; set; } = 500;

    /// <summary>
    /// Maximum allowed hybrid search limit. Default: 100.
    /// </summary>
    public int MaxHybridSearchLimit { get; set; } = 100;
}
