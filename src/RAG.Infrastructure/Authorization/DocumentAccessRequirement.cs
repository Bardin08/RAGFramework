using Microsoft.AspNetCore.Authorization;
using RAG.Core.Domain.Enums;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Authorization requirement for document access with a specific permission level.
/// </summary>
public class DocumentAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The required permission level for the document operation.
    /// </summary>
    public PermissionType RequiredPermission { get; }

    public DocumentAccessRequirement(PermissionType requiredPermission)
    {
        RequiredPermission = requiredPermission;
    }

    /// <summary>
    /// Requirement for read access to a document.
    /// </summary>
    public static DocumentAccessRequirement Read => new(PermissionType.Read);

    /// <summary>
    /// Requirement for write access to a document.
    /// </summary>
    public static DocumentAccessRequirement Write => new(PermissionType.Write);

    /// <summary>
    /// Requirement for admin access to a document.
    /// </summary>
    public static DocumentAccessRequirement Admin => new(PermissionType.Admin);
}
