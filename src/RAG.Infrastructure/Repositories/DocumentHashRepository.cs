using Microsoft.EntityFrameworkCore;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for DocumentHash entity.
/// </summary>
public class DocumentHashRepository(ApplicationDbContext context) : IDocumentHashRepository
{
    private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Gets a document hash by hash value and tenant ID.
    /// </summary>
    public async Task<DocumentHash?> GetByHashAsync(string hash, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentHashes
            .FirstOrDefaultAsync(dh => dh.Hash == hash && dh.TenantId == tenantId, cancellationToken);
    }

    /// <summary>
    /// Adds a new document hash record.
    /// </summary>
    public async Task AddAsync(DocumentHash documentHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentHash);

        _context.DocumentHashes.Add(documentHash);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tries to add a new document hash record, handling race conditions.
    /// </summary>
    public async Task<bool> TryAddAsync(DocumentHash documentHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentHash);

        try
        {
            _context.DocumentHashes.Add(documentHash);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Unique constraint violation - another upload won the race
            return false;
        }
    }

    /// <summary>
    /// Checks if a document hash exists for the given hash and tenant.
    /// </summary>
    public async Task<bool> ExistsAsync(string hash, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentHashes
            .AnyAsync(dh => dh.Hash == hash && dh.TenantId == tenantId, cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation error code
        return ex.InnerException?.Message.Contains("23505") == true ||
               ex.InnerException?.Message.Contains("duplicate key") == true;
    }
}
