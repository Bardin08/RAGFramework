namespace RAG.Core.Authorization;

/// <summary>
/// Defines authorization policy names used throughout the application.
/// Use these constants when applying [Authorize(Policy = ...)] attributes.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy requiring the 'admin' role.
    /// Use for administrative operations: document management, system configuration.
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Policy requiring either 'user' or 'admin' role.
    /// Use for standard user operations: querying, reading documents.
    /// </summary>
    public const string UserOrAdmin = "UserOrAdmin";

    /// <summary>
    /// Policy requiring tenant ownership verification for resource access.
    /// Use for resource-based authorization where tenant isolation is required.
    /// </summary>
    public const string SameTenant = "SameTenant";
}
