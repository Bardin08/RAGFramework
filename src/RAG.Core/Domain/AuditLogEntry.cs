namespace RAG.Core.Domain;

/// <summary>
/// Represents an audit log entry for tracking administrative operations.
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the actor.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Username of the actor.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// The action performed (e.g., "ClearCache", "RebuildIndex").
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// The resource path that was accessed.
    /// </summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>
    /// Additional details about the action (JSON format).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// IP address of the client.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// HTTP status code of the response.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Duration of the operation in milliseconds.
    /// </summary>
    public long? DurationMs { get; init; }
}
