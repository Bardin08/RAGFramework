using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for seed dataset persistence.
/// </summary>
public class SeedDatasetRepository : ISeedDatasetRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeedDatasetRepository> _logger;

    public SeedDatasetRepository(
        ApplicationDbContext context,
        ILogger<SeedDatasetRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SeedDataset?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.SeedDatasets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeedDataset> CreateAsync(SeedDataset dataset, CancellationToken cancellationToken = default)
    {
        _context.SeedDatasets.Add(dataset);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created seed dataset record '{Name}' with {DocumentCount} documents",
            dataset.Name, dataset.DocumentsCount);

        return dataset;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var dataset = await _context.SeedDatasets
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

        if (dataset == null)
            return false;

        _context.SeedDatasets.Remove(dataset);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted seed dataset record '{Name}'", name);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SeedDataset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SeedDatasets
            .AsNoTracking()
            .OrderByDescending(s => s.LoadedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearTenantDataAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing existing documents for tenant {TenantId}", tenantId);

        // Delete document chunks
        var chunksToDelete = await _context.DocumentChunks
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (chunksToDelete.Any())
        {
            _context.DocumentChunks.RemoveRange(chunksToDelete);
        }

        // Delete documents
        var docsToDelete = await _context.Documents
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (docsToDelete.Any())
        {
            _context.Documents.RemoveRange(docsToDelete);
        }

        // Delete document hashes
        var hashesToDelete = await _context.DocumentHashes
            .Where(h => h.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (hashesToDelete.Any())
        {
            _context.DocumentHashes.RemoveRange(hashesToDelete);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cleared {DocumentCount} documents, {ChunkCount} chunks, {HashCount} hashes",
            docsToDelete.Count, chunksToDelete.Count, hashesToDelete.Count);
    }
}
