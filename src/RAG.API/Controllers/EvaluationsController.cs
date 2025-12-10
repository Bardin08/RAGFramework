using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Filters;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.DTOs.Evaluation;
using RAG.Core.Exceptions;
using RAG.Evaluation.Export;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using System.Text.Json;

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
    private readonly IEvaluationRunRepository _runRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EvaluationsController> _logger;
    private readonly ResultsExporterFactory _exporterFactory;

    public EvaluationsController(
        IEvaluationService evaluationService,
        IEvaluationRunRepository runRepository,
        ITenantContext tenantContext,
        ILogger<EvaluationsController> logger)
    {
        _evaluationService = evaluationService;
        _runRepository = runRepository;
        _tenantContext = tenantContext;
        _logger = logger;
        _exporterFactory = new ResultsExporterFactory();
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

    /// <summary>
    /// Export evaluation results in the specified format.
    /// </summary>
    /// <param name="runId">The evaluation run ID.</param>
    /// <param name="format">Export format (csv, json, markdown/md).</param>
    /// <param name="perQuery">Include per-query breakdown.</param>
    /// <param name="includePercentiles">Include percentile statistics (P50, P95, P99).</param>
    /// <param name="includeConfig">Include configuration details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Results exported successfully</response>
    /// <response code="400">Invalid format or run not completed</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">Evaluation run not found</response>
    [HttpGet("runs/{runId:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportResults(
        Guid runId,
        [FromQuery] string format = "json",
        [FromQuery] bool perQuery = false,
        [FromQuery] bool includePercentiles = true,
        [FromQuery] bool includeConfig = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Exporting results for run {RunId} in {Format} format",
            runId, format);

        // Validate format
        if (!_exporterFactory.IsFormatSupported(format))
        {
            var supportedFormats = string.Join(", ", _exporterFactory.GetSupportedFormats());
            return BadRequest(new
            {
                error = "invalid_format",
                message = $"Format '{format}' is not supported. Supported formats: {supportedFormats}"
            });
        }

        // Get the run
        var run = await _runRepository.GetByIdAsync(runId, cancellationToken);
        if (run == null)
        {
            throw new NotFoundException("Evaluation run", runId);
        }

        // Ensure run is completed
        if (run.Status != RAG.Core.Domain.EvaluationRunStatus.Completed)
        {
            return BadRequest(new
            {
                error = "run_not_completed",
                message = $"Cannot export results for a run with status '{run.Status}'. Only completed runs can be exported.",
                status = run.Status.ToString()
            });
        }

        // Reconstruct EvaluationReport from stored data
        var report = await ReconstructReportAsync(run, cancellationToken);

        // Export
        var exporter = _exporterFactory.GetExporter(format);
        var options = new ExportOptions
        {
            IncludePerQueryBreakdown = perQuery,
            IncludePercentiles = includePercentiles,
            IncludeConfiguration = includeConfig,
            DatasetName = run.Name,
            PrettyPrint = true
        };

        var data = await exporter.ExportAsync(report, options);
        var fileName = $"evaluation-{runId.ToString()[..8]}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{exporter.FileExtension}";

        return File(data, exporter.ContentType, fileName);
    }

    /// <summary>
    /// Export comparison of multiple evaluation runs.
    /// </summary>
    /// <param name="request">The comparison request with run IDs.</param>
    /// <param name="format">Export format (csv, json, markdown/md).</param>
    /// <param name="includePercentiles">Include percentile statistics.</param>
    /// <param name="includeConfig">Include configuration details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Comparison exported successfully</response>
    /// <response code="400">Invalid format or request</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    /// <response code="404">One or more runs not found</response>
    [HttpPost("runs/compare/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportComparison(
        [FromBody] CompareRunsRequest request,
        [FromQuery] string format = "json",
        [FromQuery] bool includePercentiles = true,
        [FromQuery] bool includeConfig = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Exporting comparison of {Count} runs in {Format} format",
            request.RunIds.Count, format);

        // Validate
        if (request.RunIds.Count < 2)
        {
            return BadRequest(new
            {
                error = "insufficient_runs",
                message = "At least 2 runs are required for comparison."
            });
        }

        if (!_exporterFactory.IsFormatSupported(format))
        {
            var supportedFormats = string.Join(", ", _exporterFactory.GetSupportedFormats());
            return BadRequest(new
            {
                error = "invalid_format",
                message = $"Format '{format}' is not supported. Supported formats: {supportedFormats}"
            });
        }

        // Get all runs and reconstruct reports
        var reports = new List<EvaluationReport>();
        foreach (var runId in request.RunIds)
        {
            var run = await _runRepository.GetByIdAsync(runId, cancellationToken);
            if (run == null)
            {
                throw new NotFoundException("Evaluation run", runId);
            }

            if (run.Status != RAG.Core.Domain.EvaluationRunStatus.Completed)
            {
                return BadRequest(new
                {
                    error = "run_not_completed",
                    message = $"Run {runId} has status '{run.Status}'. Only completed runs can be compared.",
                    runId = runId,
                    status = run.Status.ToString()
                });
            }

            var report = await ReconstructReportAsync(run, cancellationToken);
            reports.Add(report);
        }

        // Export comparison
        var exporter = _exporterFactory.GetExporter(format);
        var options = new ExportOptions
        {
            IncludePercentiles = includePercentiles,
            IncludeConfiguration = includeConfig,
            PrettyPrint = true
        };

        var data = await exporter.ExportComparisonAsync(reports, options);
        var fileName = $"evaluation-comparison-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{exporter.FileExtension}";

        return File(data, exporter.ContentType, fileName);
    }

    /// <summary>
    /// Reconstructs an EvaluationReport from a stored EvaluationRun.
    /// </summary>
    private async Task<EvaluationReport> ReconstructReportAsync(
        RAG.Core.Domain.EvaluationRun run,
        CancellationToken cancellationToken)
    {
        // Get metrics
        var metrics = await _runRepository.GetMetricsForRunAsync(run.Id, cancellationToken);

        // Parse configuration
        var configuration = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(run.Configuration))
        {
            try
            {
                var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(run.Configuration);
                if (configDict != null)
                {
                    foreach (var (key, value) in configDict)
                    {
                        configuration[key] = value.ValueKind switch
                        {
                            JsonValueKind.String => value.GetString() ?? "",
                            JsonValueKind.Number => value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => value.ToString()
                        };
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse configuration for run {RunId}", run.Id);
            }
        }

        // Build statistics from metrics
        var statistics = new Dictionary<string, MetricStatistics>();
        foreach (var metric in metrics)
        {
            // Parse metadata to get full statistics
            var metadata = ParseMetricMetadata(metric.Metadata);

            statistics[metric.MetricName] = new MetricStatistics(
                metric.MetricName,
                (double)metric.MetricValue,
                metadata.StandardDeviation,
                metadata.Min,
                metadata.Max,
                metadata.SuccessCount,
                metadata.FailureCount
            );
        }

        // Create report
        return new EvaluationReport
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.FinishedAt ?? DateTimeOffset.UtcNow,
            SampleCount = run.TotalQueries,
            Results = [], // We don't store individual results, only aggregated metrics
            Statistics = statistics,
            Configuration = configuration
        };
    }

    private static (double StandardDeviation, double Min, double Max, int SuccessCount, int FailureCount) ParseMetricMetadata(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return (0, 0, 0, 0, 0);
        }

        try
        {
            var doc = JsonDocument.Parse(metadata);
            var root = doc.RootElement;

            return (
                root.TryGetProperty("StandardDeviation", out var sd) ? sd.GetDouble() : 0,
                root.TryGetProperty("Min", out var min) ? min.GetDouble() : 0,
                root.TryGetProperty("Max", out var max) ? max.GetDouble() : 0,
                root.TryGetProperty("SuccessCount", out var sc) ? sc.GetInt32() : 0,
                root.TryGetProperty("FailureCount", out var fc) ? fc.GetInt32() : 0
            );
        }
        catch (JsonException)
        {
            return (0, 0, 0, 0, 0);
        }
    }
}

/// <summary>
/// Request for comparing multiple evaluation runs.
/// </summary>
public class CompareRunsRequest
{
    /// <summary>
    /// List of run IDs to compare.
    /// </summary>
    public List<Guid> RunIds { get; set; } = new();
}
