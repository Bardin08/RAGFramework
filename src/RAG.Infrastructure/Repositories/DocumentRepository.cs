using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository for document data access operations.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(
        ApplicationDbContext context,
        ILogger<DocumentRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<(List<Document> Documents, int TotalCount)> GetDocumentsAsync(
        Guid tenantId,
        int skip,
        int take,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Documents
            .Where(d => d.TenantId == tenantId);

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(d => d.Title.Contains(searchTerm));
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Get paginated results, ordered by most recent first
        var documents = await query
            .OrderByDescending(d => d.Id) // Using Id as proxy for creation time
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} documents for tenant {TenantId} (total: {TotalCount}, search: {SearchTerm})",
            documents.Count, tenantId, totalCount, searchTerm ?? "none");

        return (documents, totalCount);
    }

    /// <inheritdoc />
    public async Task<Document?> GetDocumentByIdAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(
                d => d.Id == documentId && d.TenantId == tenantId,
                cancellationToken);

        if (document == null)
        {
            _logger.LogDebug(
                "Document {DocumentId} not found for tenant {TenantId}",
                documentId, tenantId);
            return null;
        }

        _logger.LogDebug(
            "Retrieved document {DocumentId} for tenant {TenantId}",
            documentId, tenantId);

        return document;
    }

    /// <inheritdoc />
    public async Task<List<DocumentChunk>> GetDocumentChunksAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && c.TenantId == tenantId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} chunks for document {DocumentId} (tenant {TenantId})",
            chunks.Count, documentId, tenantId);

        return chunks;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // First, verify the document exists and belongs to the tenant
        var document = await _context.Documents
            .FirstOrDefaultAsync(
                d => d.Id == documentId && d.TenantId == tenantId,
                cancellationToken);

        if (document == null)
        {
            _logger.LogWarning(
                "Attempted to delete non-existent document {DocumentId} for tenant {TenantId}",
                documentId, tenantId);
            return false;
        }

        // Delete all chunks for this document
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        _context.DocumentChunks.RemoveRange(chunks);

        // Delete the document
        _context.Documents.Remove(document);

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted document {DocumentId} and {ChunkCount} chunks from database (tenant {TenantId})",
            documentId, chunks.Count, tenantId);

        return true;
    }

    /// <inheritdoc />
    public async Task AddDocumentWithChunksAsync(
        Document document,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chunks);

        // Add document and chunks to context
        _context.Documents.Add(document);
        _context.DocumentChunks.AddRange(chunks);

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added document {DocumentId} with {ChunkCount} chunks to database (tenant {TenantId})",
            document.Id, chunks.Count, document.TenantId);
    }
}
