namespace RAG.API.Models.Responses;

/// <summary>
/// DTO for user information used in sharing autocomplete.
/// </summary>
public class UserDto
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's username (if different from email).
    /// </summary>
    public string? Username { get; set; }
}
