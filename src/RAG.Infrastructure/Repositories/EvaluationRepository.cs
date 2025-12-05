using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Infrastructure.Data;
using EvaluationEntity = RAG.Core.Domain.Evaluation;

namespace RAG.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for evaluation configuration persistence.
/// </summary>
public class EvaluationRepository : IEvaluationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EvaluationRepository> _logger;

    public EvaluationRepository(
        ApplicationDbContext context,
        ILogger<EvaluationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EvaluationEntity> CreateAsync(EvaluationEntity evaluation, CancellationToken cancellationToken = default)
    {
        _context.Evaluations.Add(evaluation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created evaluation {EvaluationId} with name '{Name}'",
            evaluation.Id, evaluation.Name);

        return evaluation;
    }

    public async Task<EvaluationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Evaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<EvaluationEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Evaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationEntity>> GetAllAsync(
        bool? isActive = null,
        string? type = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Evaluations.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(e => e.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.Type == type);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        bool? isActive = null,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Evaluations.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(e => e.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.Type == type);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<EvaluationEntity> UpdateAsync(EvaluationEntity evaluation, CancellationToken cancellationToken = default)
    {
        _context.Evaluations.Update(evaluation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated evaluation {EvaluationId}", evaluation.Id);

        return evaluation;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evaluation = await _context.Evaluations
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (evaluation == null)
            return false;

        // Soft delete
        evaluation.IsActive = false;
        evaluation.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted evaluation {EvaluationId}", id);

        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evaluation = await _context.Evaluations
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (evaluation == null)
            return false;

        // Delete all associated runs first (cascade should handle this, but being explicit)
        var runs = await _context.EvaluationRuns
            .Where(r => r.EvaluationId == id)
            .ToListAsync(cancellationToken);

        if (runs.Any())
        {
            _context.EvaluationRuns.RemoveRange(runs);
        }

        _context.Evaluations.Remove(evaluation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Hard deleted evaluation {EvaluationId} and {RunCount} associated runs",
            id, runs.Count);

        return true;
    }

    public async Task<bool> IsNameUniqueAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Evaluations.Where(e => e.Name == name);

        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);

        return !await query.AnyAsync(cancellationToken);
    }
}
