namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Request to load a seed dataset.
/// </summary>
public class SeedDataRequest
{
    /// <summary>
    /// Name of the seed dataset to load (e.g., "dev-seed", "test-seed").
    /// Required if DatasetJson is not provided.
    /// </summary>
    public string? DatasetName { get; set; }

    /// <summary>
    /// Inline JSON data for the seed dataset.
    /// Required if DatasetName is not provided.
    /// </summary>
    public string? DatasetJson { get; set; }

    /// <summary>
    /// Tenant ID to use for loading documents.
    /// If not specified, will use a default evaluation tenant.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Force reload even if the dataset is already loaded with the same hash.
    /// Default is false.
    /// </summary>
    public bool ForceReload { get; set; } = false;

    /// <summary>
    /// Validates the request.
    /// </summary>
    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(DatasetName) && string.IsNullOrWhiteSpace(DatasetJson))
        {
            return (false, "Either DatasetName or DatasetJson must be provided");
        }

        if (!string.IsNullOrWhiteSpace(DatasetName) && !string.IsNullOrWhiteSpace(DatasetJson))
        {
            return (false, "Only one of DatasetName or DatasetJson should be provided, not both");
        }

        return (true, null);
    }
}
