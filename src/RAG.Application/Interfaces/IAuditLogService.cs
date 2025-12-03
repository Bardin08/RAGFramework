using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service interface for audit logging operations.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs an audit entry.
    /// </summary>
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs with filtering and pagination.
    /// </summary>
    Task<PagedResult<AuditLogEntry>> GetLogsAsync(
        AuditLogFilter filter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter criteria for audit log queries.
/// </summary>
public class AuditLogFilter
{
    /// <summary>
    /// Filter by user ID.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Filter by action type.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Filter by resource path.
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Start date for filtering.
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// End date for filtering.
    /// </summary>
    public DateTime? ToDate { get; init; }
}

/// <summary>
/// Paginated result wrapper.
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// Items in the current page.
    /// </summary>
    public List<T> Items { get; init; } = new();

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
