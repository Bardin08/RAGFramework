namespace RAG.Application.Interfaces;

/// <summary>
/// Service for looking up users for sharing autocomplete.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Searches for users within the same tenant by email or username.
    /// </summary>
    /// <param name="tenantId">The tenant ID to search within.</param>
    /// <param name="searchTerm">The search term (email or username prefix).</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching users.</returns>
    Task<List<UserInfo>> SearchUsersAsync(
        Guid tenantId,
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// User information returned from user lookup.
/// </summary>
public record UserInfo(Guid Id, string Name, string Email, string? Username);
