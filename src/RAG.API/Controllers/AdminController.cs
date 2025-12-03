using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Filters;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.DTOs.Admin;

namespace RAG.API.Controllers;

/// <summary>
/// Administrative endpoints for system management.
/// Requires Admin role for all operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Produces("application/json")]
[AuditLog]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// Get system statistics including document counts, cache hit rate, and uptime.
    /// </summary>
    /// <remarks>
    /// Returns aggregated statistics about the RAG system including:
    /// - Total documents and chunks
    /// - Query count and cache hit rate
    /// - System uptime
    /// - Documents grouped by tenant
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SystemStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin stats requested by {User}", User.Identity?.Name);

        var stats = await _adminService.GetSystemStatsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Start an index rebuild operation for all or specific tenant documents.
    /// </summary>
    /// <remarks>
    /// Queues a background job to rebuild the search index.
    /// Returns immediately with a job ID for tracking progress.
    ///
    /// Use GET /api/admin/index/rebuild/{jobId} to check job status.
    /// </remarks>
    /// <param name="request">Rebuild configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="202">Rebuild job queued successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpPost("index/rebuild")]
    [ProducesResponseType(typeof(IndexRebuildResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IndexRebuildResponse>> RebuildIndex(
        [FromBody] IndexRebuildRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Index rebuild requested by {User}. TenantId: {TenantId}",
            User.Identity?.Name,
            request.TenantId?.ToString() ?? "all");

        var result = await _adminService.StartIndexRebuildAsync(request, cancellationToken);
        return Accepted(result);
    }

    /// <summary>
    /// Get the status of an index rebuild job.
    /// </summary>
    /// <param name="jobId">The job ID returned from the rebuild request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Job status retrieved successfully</response>
    /// <response code="404">Job not found</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("index/rebuild/{jobId:guid}")]
    [ProducesResponseType(typeof(IndexRebuildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IndexRebuildResponse>> GetRebuildStatus(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetRebuildStatusAsync(jobId, cancellationToken);

        if (result == null)
        {
            return NotFound(new { message = $"Job {jobId} not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get detailed health status of all system dependencies.
    /// </summary>
    /// <remarks>
    /// Returns comprehensive health information for each dependency:
    /// - PostgreSQL database
    /// - Elasticsearch
    /// - Qdrant vector store
    /// - Embedding service
    /// - Keycloak (if configured)
    /// - LLM providers
    ///
    /// Each dependency includes status, response time, and version where available.
    /// </remarks>
    /// <response code="200">Health status retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("health/detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DetailedHealthResponse>> GetDetailedHealth(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Detailed health check requested by {User}", User.Identity?.Name);

        var health = await _adminService.GetDetailedHealthAsync(cancellationToken);
        return Ok(health);
    }

    /// <summary>
    /// Clear system caches.
    /// </summary>
    /// <remarks>
    /// Clears specified cache types or all caches if "all" is specified.
    ///
    /// Available cache types:
    /// - query: Query result cache
    /// - embedding: Embedding cache
    /// - token: Token cache
    /// - health: Health check cache
    /// - all: Clear all caches
    /// </remarks>
    /// <param name="request">Cache types to clear</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Cache cleared successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpDelete("cache/clear")]
    [ProducesResponseType(typeof(CacheClearResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CacheClearResponse>> ClearCache(
        [FromBody] CacheClearRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cache clear requested by {User}. Types: {CacheTypes}",
            User.Identity?.Name,
            string.Join(", ", request.CacheTypes));

        var result = await _adminService.ClearCacheAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get audit logs with filtering and pagination.
    /// </summary>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="action">Filter by action type</param>
    /// <param name="fromDate">Filter from date (inclusive)</param>
    /// <param name="toDate">Filter to date (inclusive)</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (max 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Audit logs retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - admin role required</response>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedResult<Core.Domain.AuditLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<Core.Domain.AuditLogEntry>>> GetAuditLogs(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var auditService = HttpContext.RequestServices.GetService<IAuditLogService>();
        if (auditService == null)
        {
            return StatusCode(500, new { message = "Audit service not available" });
        }

        pageSize = Math.Min(pageSize, 100); // Cap at 100

        var filter = new AuditLogFilter
        {
            UserId = userId,
            Action = action,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await auditService.GetLogsAsync(filter, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
