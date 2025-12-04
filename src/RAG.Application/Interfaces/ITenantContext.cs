namespace RAG.Application.Interfaces;

/// <summary>
/// Provides access to the current tenant and user context for the request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID.
    /// </summary>
    /// <returns>The tenant ID for the current request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant context is not available.</exception>
    Guid GetTenantId();

    /// <summary>
    /// Tries to get the current tenant ID.
    /// </summary>
    /// <param name="tenantId">The tenant ID if available.</param>
    /// <returns>True if tenant ID is available, false otherwise.</returns>
    bool TryGetTenantId(out Guid tenantId);

    /// <summary>
    /// Gets the current user ID.
    /// </summary>
    /// <returns>The user ID for the current request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user context is not available.</exception>
    Guid GetUserId();

    /// <summary>
    /// Tries to get the current user ID.
    /// </summary>
    /// <param name="userId">The user ID if available.</param>
    /// <returns>True if user ID is available, false otherwise.</returns>
    bool TryGetUserId(out Guid userId);

    /// <summary>
    /// Gets a value indicating whether the current user has global admin access.
    /// </summary>
    bool IsGlobalAdmin { get; }
}
