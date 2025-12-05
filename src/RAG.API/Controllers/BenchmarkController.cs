using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Filters;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.DTOs.Benchmark;

namespace RAG.API.Controllers;

/// <summary>
/// Benchmark endpoints for automated testing and CI/CD integration.
/// Requires Admin role for all operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/benchmark")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Produces("application/json")]
[AuditLog]
public class BenchmarkController : ControllerBase
{
    private readonly IBenchmarkService _benchmarkService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BenchmarkController> _logger;

    public BenchmarkController(
        IBenchmarkService benchmarkService,
        ITenantContext tenantContext,
        ILogger<BenchmarkController> logger)
    {
        _benchmarkService = benchmarkService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Create a new benchmark job.
    /// </summary>
    /// <remarks>
    /// Creates and queues a benchmark job to evaluate the RAG system against a specified dataset.
    ///
    /// The benchmark will:
    /// - Load the specified dataset (from data/seeds/ directory)
    /// - Run configured evaluation metrics
    /// - Optionally limit the number of samples tested
    /// - Store results for later retrieval
    ///
    /// Returns immediately with a job ID for tracking progress.
    /// Use GET /api/admin/benchmark/{jobId} to check status.
    /// </remarks>
    /// <param name="request">Benchmark configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="202">Benchmark job created and queued successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(BenchmarkJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkJobResponse>> CreateBenchmark(
        [FromBody] BenchmarkRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _tenantContext.TryGetUserId(out var uid) ? uid.ToString() : "system";

        _logger.LogInformation(
            "Benchmark requested by {UserId} ({UserName}). Dataset: {Dataset}",
            userId,
            User.Identity?.Name,
            request.Dataset);

        var result = await _benchmarkService.CreateJobAsync(request, userId, cancellationToken);
        return Accepted(result);
    }

    /// <summary>
    /// Get the status of a benchmark job.
    /// </summary>
    /// <param name="jobId">The job ID returned from the benchmark request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Job status retrieved successfully</response>
    /// <response code="404">Job not found</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(BenchmarkJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkJobResponse>> GetJobStatus(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _benchmarkService.GetJobAsync(jobId, cancellationToken);

        if (result == null)
        {
            return NotFound(new { message = $"Benchmark job {jobId} not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get the results of a completed benchmark job.
    /// </summary>
    /// <remarks>
    /// Returns detailed benchmark results including:
    /// - Aggregated metrics with summary statistics (mean, std dev, min, max)
    /// - Per-metric breakdowns
    /// - Optional per-sample detailed results
    ///
    /// Only available for completed jobs.
    /// </remarks>
    /// <param name="jobId">The job ID.</param>
    /// <param name="includeDetails">Whether to include per-sample detailed results (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Results retrieved successfully</response>
    /// <response code="404">Job not found or not completed yet</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("{jobId:guid}/results")]
    [ProducesResponseType(typeof(BenchmarkResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BenchmarkResultsResponse>> GetResults(
        Guid jobId,
        [FromQuery] bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _benchmarkService.GetResultsAsync(jobId, includeDetails, cancellationToken);

        if (result == null)
        {
            return NotFound(new
            {
                message = $"Results for benchmark job {jobId} not found. " +
                         "Job may not exist or may not be completed yet."
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// List all benchmark jobs.
    /// </summary>
    /// <remarks>
    /// Returns a list of all benchmark jobs, optionally filtered by status or user.
    ///
    /// Results are ordered by creation date (most recent first) and limited to the specified count.
    /// </remarks>
    /// <param name="status">Optional status filter (Created, Queued, Running, Completed, Failed).</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="limit">Maximum number of results (default: 50, max: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<BenchmarkJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<BenchmarkJobResponse>>> ListJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? userId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // Cap limit at 100
        limit = Math.Min(limit, 100);

        _logger.LogInformation(
            "Listing benchmark jobs. Status: {Status}, UserId: {UserId}, Limit: {Limit}",
            status ?? "all",
            userId ?? "all",
            limit);

        var jobs = await _benchmarkService.ListJobsAsync(status, userId, limit, cancellationToken);
        return Ok(jobs);
    }

    /// <summary>
    /// Export benchmark results in the specified format.
    /// </summary>
    /// <remarks>
    /// Exports benchmark results in various formats for analysis and reporting.
    ///
    /// Supported formats:
    /// - **csv**: Comma-separated values with metric statistics
    /// - **json**: Full JSON export with all metadata
    /// - **md** or **markdown**: Formatted markdown report
    ///
    /// The response is a file download with appropriate content-type headers.
    /// </remarks>
    /// <param name="jobId">The job ID.</param>
    /// <param name="format">Export format: csv, json, or md (default: json).</param>
    /// <param name="perQuery">Include per-query breakdown (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Export file returned successfully</response>
    /// <response code="404">Job not found or not completed</response>
    /// <response code="400">Invalid export format</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("{jobId:guid}/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportResults(
        Guid jobId,
        [FromQuery] string format = "json",
        [FromQuery] bool perQuery = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Export requested for benchmark job {JobId}. Format: {Format}, PerQuery: {PerQuery}",
            jobId,
            format,
            perQuery);

        // Validate format
        var validFormats = new[] { "csv", "json", "md", "markdown" };
        if (!validFormats.Contains(format.ToLowerInvariant()))
        {
            return BadRequest(new
            {
                message = $"Invalid export format '{format}'. Supported formats: {string.Join(", ", validFormats)}"
            });
        }

        var result = await _benchmarkService.ExportResultsAsync(jobId, format, perQuery, cancellationToken);

        if (result == null)
        {
            return NotFound(new
            {
                message = $"Cannot export benchmark job {jobId}. " +
                         "Job may not exist, may not be completed, or may have no results."
            });
        }

        return File(result.Data, result.ContentType, result.FileName);
    }
}
