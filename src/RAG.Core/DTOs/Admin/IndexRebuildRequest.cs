namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Request to rebuild the document index.
/// </summary>
public class IndexRebuildRequest
{
    /// <summary>
    /// Optional tenant ID to rebuild index for. If null, rebuilds for all tenants.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Whether to regenerate embeddings during rebuild. Defaults to true.
    /// </summary>
    public bool IncludeEmbeddings { get; init; } = true;
}
