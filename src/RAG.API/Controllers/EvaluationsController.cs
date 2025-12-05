using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Filters;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.DTOs.Evaluation;
using RAG.Core.Exceptions;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for managing evaluation configurations and running evaluations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/evaluations")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Produces("application/json")]
[AuditLog]
public class EvaluationsController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EvaluationsController> _logger;

    public EvaluationsController(
        IEvaluationService evaluationService,
        ITenantContext tenantContext,
        ILogger<EvaluationsController> logger)
    {
        _evaluationService = evaluationService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Create a new evaluation configuration.
    /// </summary>
    /// <param name="request">The evaluation configuration to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Evaluation created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="409">Evaluation with this name already exists</response>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvaluationResponse>> CreateEvaluation(
        [FromBody] CreateEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _tenantContext.GetUserId();

        _logger.LogInformation(
            "Creating evaluation '{Name}' of type '{Type}' by user {UserId}",
            request.Name, request.Type, userId);

        var evaluation = await _evaluationService.CreateEvaluationAsync(
            request, userId, cancellationToken);

        var response = new EvaluationResponse
        {
            Id = evaluation.Id,
            Name = evaluation.Name,
            Description = evaluation.Description,
            Type = evaluation.Type,
            Config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evaluation.Config),
            CreatedAt = evaluation.CreatedAt,
            CreatedBy = evaluation.CreatedBy,
            IsActive = evaluation.IsActive,
            UpdatedAt = evaluation.UpdatedAt,
            UpdatedBy = evaluation.UpdatedBy
        };

        return CreatedAtAction(
            nameof(GetEvaluation),
            new { id = response.Id },
            response);
    }

    /// <summary>
    /// Get all evaluation configurations with optional filtering.
    /// </summary>
    /// <param name="isActive">Filter by active status.</param>
    /// <param name="type">Filter by evaluation type.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (max 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Evaluations retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(EvaluationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EvaluationListResponse>> GetEvaluations(
        [FromQuery] bool? isActive,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _evaluationService.GetEvaluationsAsync(
            isActive, type, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get a specific evaluation by ID.
    /// </summary>
    /// <param name="id">The evaluation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Evaluation retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">Evaluation not found</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationResponse>> GetEvaluation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var evaluation = await _evaluationService.GetEvaluationAsync(id, cancellationToken);

        if (evaluation == null)
        {
            throw new NotFoundException("Evaluation", id);
        }

        var response = new EvaluationResponse
        {
            Id = evaluation.Id,
            Name = evaluation.Name,
            Description = evaluation.Description,
            Type = evaluation.Type,
            Config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evaluation.Config),
            CreatedAt = evaluation.CreatedAt,
            CreatedBy = evaluation.CreatedBy,
            IsActive = evaluation.IsActive,
            UpdatedAt = evaluation.UpdatedAt,
            UpdatedBy = evaluation.UpdatedBy
        };

        return Ok(response);
    }

    /// <summary>
    /// Update an evaluation configuration.
    /// </summary>
    /// <param name="id">The evaluation ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Evaluation updated successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">Evaluation not found</response>
    /// <response code="409">Evaluation name conflict</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvaluationResponse>> UpdateEvaluation(
        Guid id,
        [FromBody] UpdateEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _tenantContext.GetUserId();

        _logger.LogInformation(
            "Updating evaluation {EvaluationId} by user {UserId}",
            id, userId);

        var evaluation = await _evaluationService.UpdateEvaluationAsync(
            id, request, userId, cancellationToken);

        var response = new EvaluationResponse
        {
            Id = evaluation.Id,
            Name = evaluation.Name,
            Description = evaluation.Description,
            Type = evaluation.Type,
            Config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evaluation.Config),
            CreatedAt = evaluation.CreatedAt,
            CreatedBy = evaluation.CreatedBy,
            IsActive = evaluation.IsActive,
            UpdatedAt = evaluation.UpdatedAt,
            UpdatedBy = evaluation.UpdatedBy
        };

        return Ok(response);
    }

    /// <summary>
    /// Delete an evaluation configuration (soft delete).
    /// </summary>
    /// <param name="id">The evaluation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Evaluation deleted successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">Evaluation not found</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvaluation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _tenantContext.GetUserId();

        _logger.LogInformation(
            "Deleting evaluation {EvaluationId} by user {UserId}",
            id, userId);

        var deleted = await _evaluationService.DeleteEvaluationAsync(id, userId, cancellationToken);

        if (!deleted)
        {
            throw new NotFoundException("Evaluation", id);
        }

        return NoContent();
    }

    /// <summary>
    /// Run an evaluation.
    /// </summary>
    /// <param name="id">The evaluation ID.</param>
    /// <param name="request">The run request with optional overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="202">Evaluation run started successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">Evaluation not found</response>
    [HttpPost("{id:guid}/run")]
    [ProducesResponseType(typeof(RunEvaluationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunEvaluationResponse>> RunEvaluation(
        Guid id,
        [FromBody] RunEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _tenantContext.GetUserId();
        var tenantId = _tenantContext.GetTenantId().ToString();

        _logger.LogInformation(
            "Starting evaluation run for {EvaluationId} by user {UserId}",
            id, userId);

        var run = await _evaluationService.RunEvaluationAsync(
            id, request, userId, tenantId, cancellationToken);

        var response = new RunEvaluationResponse
        {
            RunId = run.Id,
            Status = run.Status.ToString(),
            Message = $"Evaluation run started. Use GET /api/evaluations/runs/{run.Id} to check status."
        };

        return Accepted(response);
    }
}
