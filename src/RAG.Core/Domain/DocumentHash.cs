namespace RAG.Core.Domain;

/// <summary>
/// Represents a SHA-256 hash of a document used for deduplication.
/// </summary>
public class DocumentHash
{
    /// <summary>
    /// Gets the unique identifier for this hash record.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash value (64-character hex string).
    /// </summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the document ID that this hash corresponds to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the original filename of the uploaded document.
    /// </summary>
    public string OriginalFileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the document was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; init; }

    /// <summary>
    /// Gets the ID of the user who uploaded the document.
    /// </summary>
    public Guid UploadedBy { get; init; }

    /// <summary>
    /// Gets the tenant ID for multi-tenancy isolation.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Creates a new instance of DocumentHash.
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="hash">SHA-256 hash value</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="originalFileName">Original filename</param>
    /// <param name="uploadedAt">Upload timestamp</param>
    /// <param name="uploadedBy">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    public DocumentHash(
        Guid id,
        string hash,
        Guid documentId,
        string originalFileName,
        DateTime uploadedAt,
        Guid uploadedBy,
        Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original filename cannot be null or empty", nameof(originalFileName));

        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));

        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty", nameof(documentId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty", nameof(tenantId));

        if (uploadedBy == Guid.Empty)
            throw new ArgumentException("UploadedBy cannot be empty", nameof(uploadedBy));

        Id = id;
        Hash = hash;
        DocumentId = documentId;
        OriginalFileName = originalFileName;
        UploadedAt = uploadedAt;
        UploadedBy = uploadedBy;
        TenantId = tenantId;
    }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private DocumentHash()
    {
    }
}
