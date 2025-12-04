namespace RAG.Application.Interfaces;

/// <summary>
/// Service for looking up user information from the identity provider.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Gets user information by user ID.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User information if found, null otherwise.</returns>
    Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user information for multiple users.
    /// </summary>
    /// <param name="userIds">The user IDs to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping user IDs to user information.</returns>
    Task<Dictionary<Guid, UserInfo>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for users by email or username within a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID to search within.</param>
    /// <param name="searchTerm">The search term (email or username prefix).</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching users.</returns>
    Task<IReadOnlyList<UserInfo>> SearchUsersAsync(
        Guid tenantId,
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// User information from the identity provider.
/// </summary>
public class UserInfo
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The user's username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The user's first name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// The user's last name.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// The user's full display name.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName)
        ? Username
        : $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Alias for DisplayName for compatibility.
    /// </summary>
    public string Name => DisplayName;
}
