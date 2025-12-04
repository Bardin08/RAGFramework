using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for document access control operations.
/// </summary>
public class DocumentAccessRepository : IDocumentAccessRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentAccessRepository> _logger;

    public DocumentAccessRepository(
        ApplicationDbContext context,
        ILogger<DocumentAccessRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DocumentAccess?> GetAccessAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentAccess
            .FirstOrDefaultAsync(
                a => a.DocumentId == documentId && a.UserId == userId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<DocumentAccess>> GetDocumentAccessListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentAccess
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.GrantedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<DocumentAccess>> GetUserAccessListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentAccess
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.GrantedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task GrantAccessAsync(
        DocumentAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);

        // Check if access already exists
        var existing = await GetAccessAsync(access.DocumentId, access.UserId, cancellationToken);

        if (existing != null)
        {
            // Remove existing and add new (to update permission level)
            _context.DocumentAccess.Remove(existing);
        }

        _context.DocumentAccess.Add(access);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Granted {Permission} access for user {UserId} to document {DocumentId}",
            access.Permission, access.UserId, access.DocumentId);
    }

    /// <inheritdoc />
    public async Task RevokeAccessAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(documentId, userId, cancellationToken);

        if (access != null)
        {
            _context.DocumentAccess.Remove(access);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Revoked access for user {UserId} from document {DocumentId}",
                userId, documentId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        Guid documentId,
        Guid userId,
        PermissionType minPermission,
        CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(documentId, userId, cancellationToken);

        if (access == null)
        {
            return false;
        }

        return access.HasPermission(minPermission);
    }

    /// <inheritdoc />
    public async Task<Document?> GetDocumentWithOwnerAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogAccessAuditAsync(
        AccessAuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        _context.AccessAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Logged access audit: {Action} by {ActorUserId} on document {DocumentId}",
            auditLog.Action, auditLog.ActorUserId, auditLog.DocumentId);
    }
}
