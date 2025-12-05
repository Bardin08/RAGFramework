using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.DTOs.Evaluation;
using RAG.Core.Exceptions;

namespace RAG.Application.Services;

/// <summary>
/// Service implementation for orchestrating evaluation operations.
/// </summary>
public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _evaluationRepository;
    private readonly IEvaluationRunRepository _runRepository;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        IEvaluationRepository evaluationRepository,
        IEvaluationRunRepository runRepository,
        ILogger<EvaluationService> logger)
    {
        _evaluationRepository = evaluationRepository;
        _runRepository = runRepository;
        _logger = logger;
    }

    public async Task<Evaluation> CreateEvaluationAsync(
        CreateEvaluationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Validate name uniqueness
        if (!await _evaluationRepository.IsNameUniqueAsync(request.Name, null, cancellationToken))
        {
            throw new ConflictException($"Evaluation with name '{request.Name}' already exists");
        }

        // Validate config is valid JSON
        string configJson;
        try
        {
            configJson = JsonSerializer.Serialize(request.Config);
        }
        catch (Exception ex)
        {
            throw new ValidationException("config", $"Invalid configuration JSON: {ex.Message}");
        }

        var evaluation = new Evaluation
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Config = configJson,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
            IsActive = true
        };

        var created = await _evaluationRepository.CreateAsync(evaluation, cancellationToken);

        _logger.LogInformation(
            "Created evaluation {EvaluationId} with name '{Name}' by user {UserId}",
            created.Id, created.Name, userId);

        return created;
    }

    public async Task<Evaluation> UpdateEvaluationAsync(
        Guid id,
        UpdateEvaluationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await _evaluationRepository.GetByIdAsync(id, cancellationToken);
        if (evaluation == null)
        {
            throw new NotFoundException("Evaluation", id);
        }

        // Update name if provided and check uniqueness
        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != evaluation.Name)
        {
            if (!await _evaluationRepository.IsNameUniqueAsync(request.Name, id, cancellationToken))
            {
                throw new ConflictException($"Evaluation with name '{request.Name}' already exists");
            }
            evaluation.Name = request.Name;
        }

        // Update other fields
        if (request.Description != null)
            evaluation.Description = request.Description;

        if (!string.IsNullOrWhiteSpace(request.Type))
            evaluation.Type = request.Type;

        if (request.Config.HasValue)
        {
            try
            {
                evaluation.Config = JsonSerializer.Serialize(request.Config.Value);
            }
            catch (Exception ex)
            {
                throw new ValidationException("config", $"Invalid configuration JSON: {ex.Message}");
            }
        }

        if (request.IsActive.HasValue)
            evaluation.IsActive = request.IsActive.Value;

        evaluation.UpdatedAt = DateTimeOffset.UtcNow;
        evaluation.UpdatedBy = userId;

        var updated = await _evaluationRepository.UpdateAsync(evaluation, cancellationToken);

        _logger.LogInformation(
            "Updated evaluation {EvaluationId} by user {UserId}",
            id, userId);

        return updated;
    }

    public async Task<Evaluation?> GetEvaluationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _evaluationRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<EvaluationListResponse> GetEvaluationsAsync(
        bool? isActive = null,
        string? type = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;

        var evaluations = await _evaluationRepository.GetAllAsync(
            isActive, type, skip, pageSize, cancellationToken);

        var total = await _evaluationRepository.GetCountAsync(
            isActive, type, cancellationToken);

        var items = evaluations.Select(e => new EvaluationResponse
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Type = e.Type,
            Config = JsonSerializer.Deserialize<JsonElement>(e.Config),
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy,
            IsActive = e.IsActive,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy
        }).ToList();

        return new EvaluationListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> DeleteEvaluationAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await _evaluationRepository.GetByIdAsync(id, cancellationToken);
        if (evaluation == null)
        {
            return false;
        }

        var deleted = await _evaluationRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation(
                "Deleted evaluation {EvaluationId} by user {UserId}",
                id, userId);
        }

        return deleted;
    }

    public async Task<EvaluationRun> RunEvaluationAsync(
        Guid evaluationId,
        RunEvaluationRequest request,
        Guid userId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await _evaluationRepository.GetByIdAsync(evaluationId, cancellationToken);
        if (evaluation == null)
        {
            throw new NotFoundException("Evaluation", evaluationId);
        }

        if (!evaluation.IsActive)
        {
            throw new ValidationException("Cannot run inactive evaluation");
        }

        // Merge base config with overrides
        var baseConfig = JsonSerializer.Deserialize<JsonElement>(evaluation.Config);
        var finalConfig = baseConfig;

        if (request.ConfigOverrides.HasValue)
        {
            // Simple merge: overrides take precedence
            // In production, implement proper JSON merge strategy
            finalConfig = request.ConfigOverrides.Value;
        }

        var run = new EvaluationRun
        {
            Id = Guid.NewGuid(),
            EvaluationId = evaluationId,
            Name = request.Name ?? $"Run {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}",
            Description = request.Description,
            StartedAt = DateTimeOffset.UtcNow,
            Status = EvaluationRunStatus.Queued,
            Progress = 0,
            Configuration = JsonSerializer.Serialize(finalConfig),
            TotalQueries = 0,
            CompletedQueries = 0,
            FailedQueries = 0,
            InitiatedBy = userId.ToString(),
            TenantId = tenantId
        };

        var created = await _runRepository.CreateAsync(run, cancellationToken);

        _logger.LogInformation(
            "Created evaluation run {RunId} for evaluation {EvaluationId} by user {UserId}",
            created.Id, evaluationId, userId);

        // TODO: Queue the actual evaluation execution as a background job
        // For now, we just create the run record

        return created;
    }
}
