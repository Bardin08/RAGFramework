using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of audit logging service.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ApplicationDbContext dbContext,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Audit: User={UserId} Action={Action} Resource={Resource} Status={StatusCode}",
            entry.UserId,
            entry.Action,
            entry.Resource,
            entry.StatusCode);

        _dbContext.AuditLogs.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditLogEntry>> GetLogsAsync(
        AuditLogFilter filter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        if (!string.IsNullOrEmpty(filter.Action))
        {
            query = query.Where(a => a.Action.Contains(filter.Action));
        }

        if (!string.IsNullOrEmpty(filter.Resource))
        {
            query = query.Where(a => a.Resource.Contains(filter.Resource));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
