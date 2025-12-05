using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository interface for evaluation configuration persistence.
/// </summary>
public interface IEvaluationRepository
{
    /// <summary>
    /// Creates a new evaluation configuration.
    /// </summary>
    Task<Evaluation> CreateAsync(Evaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an evaluation by ID.
    /// </summary>
    Task<Evaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an evaluation by name.
    /// </summary>
    Task<Evaluation?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all evaluations with optional filtering.
    /// </summary>
    Task<IReadOnlyList<Evaluation>> GetAllAsync(
        bool? isActive = null,
        string? type = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of evaluations for pagination.
    /// </summary>
    Task<int> GetCountAsync(
        bool? isActive = null,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing evaluation.
    /// </summary>
    Task<Evaluation> UpdateAsync(Evaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an evaluation (soft delete by setting IsActive = false).
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard deletes an evaluation and all associated runs.
    /// </summary>
    Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an evaluation name is unique.
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
