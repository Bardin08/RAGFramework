namespace RAG.Core.Domain.Enums;

/// <summary>
/// Permission levels for document access.
/// Higher values include lower permissions (Admin > Write > Read).
/// </summary>
public enum PermissionType
{
    /// <summary>
    /// Read-only access to the document.
    /// </summary>
    Read = 1,

    /// <summary>
    /// Read and write access to the document.
    /// </summary>
    Write = 2,

    /// <summary>
    /// Full administrative access including sharing capabilities.
    /// </summary>
    Admin = 3
}
