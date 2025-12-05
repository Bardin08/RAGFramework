using RAG.Core.Domain;
using RAG.Core.DTOs.Evaluation;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for orchestrating evaluation operations.
/// </summary>
public interface IEvaluationService
{
    /// <summary>
    /// Creates a new evaluation configuration.
    /// </summary>
    Task<Evaluation> CreateEvaluationAsync(
        CreateEvaluationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing evaluation configuration.
    /// </summary>
    Task<Evaluation> UpdateEvaluationAsync(
        Guid id,
        UpdateEvaluationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an evaluation by ID.
    /// </summary>
    Task<Evaluation?> GetEvaluationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all evaluations with optional filtering.
    /// </summary>
    Task<EvaluationListResponse> GetEvaluationsAsync(
        bool? isActive = null,
        string? type = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an evaluation (soft delete).
    /// </summary>
    Task<bool> DeleteEvaluationAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an evaluation and creates a new evaluation run.
    /// </summary>
    Task<EvaluationRun> RunEvaluationAsync(
        Guid evaluationId,
        RunEvaluationRequest request,
        Guid userId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
