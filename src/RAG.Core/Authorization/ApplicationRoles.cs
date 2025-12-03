namespace RAG.Core.Authorization;

/// <summary>
/// Defines application role names that map to Keycloak realm roles.
/// Role names are case-sensitive and must match Keycloak configuration exactly.
/// </summary>
public static class ApplicationRoles
{
    /// <summary>
    /// Administrator role with full system access.
    /// Maps to Keycloak realm role: 'admin'.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Standard user role with read/query access.
    /// Maps to Keycloak realm role: 'user'.
    /// </summary>
    public const string User = "user";
}
