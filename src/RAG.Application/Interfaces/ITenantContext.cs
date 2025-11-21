namespace RAG.Application.Interfaces;

/// <summary>
/// Service for managing tenant context from JWT claims.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID from the JWT token.
    /// </summary>
    /// <returns>The tenant ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant_id claim is missing.</exception>
    Guid GetCurrentTenantId();
}
