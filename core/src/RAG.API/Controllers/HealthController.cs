using Microsoft.AspNetCore.Mvc;
using RAG.Application.Services;
using RAG.Core.Domain;

namespace RAG.API.Controllers;

/// <summary>
/// Health check endpoints for monitoring and orchestration
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthCheckService healthCheckService,
        ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe for Kubernetes
    /// </summary>
    /// <remarks>
    /// Returns 200 OK if the application is running
    /// </remarks>
    /// <response code="200">Application is alive</response>
    /// <response code="500">Application is not responding</response>
    [HttpGet("/healthz")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Liveness()
    {
        return Ok("OK");
    }

    /// <summary>
    /// Liveness probe alias
    /// </summary>
    /// <remarks>
    /// Alternative liveness probe endpoint. Returns 200 OK if the application is running
    /// </remarks>
    /// <response code="200">Application is alive</response>
    /// <response code="500">Application is not responding</response>
    [HttpGet("/healthz/live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult LivenessAlias()
    {
        return Ok("OK");
    }

    /// <summary>
    /// Readiness probe for Kubernetes
    /// </summary>
    /// <remarks>
    /// Returns 200 OK if all services are healthy, 503 Service Unavailable otherwise
    /// </remarks>
    /// <response code="200">All services are ready</response>
    /// <response code="503">One or more services are unhealthy</response>
    [HttpGet("/healthz/ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Readiness()
    {
        var health = await _healthCheckService.GetHealthStatusAsync();
        return health.Status == "Healthy" ? Ok() : StatusCode(503);
    }

    /// <summary>
    /// Detailed health status of all services
    /// </summary>
    /// <remarks>
    /// Returns detailed JSON with health status of all RAG system dependencies. Requires authentication in production.
    /// </remarks>
    /// <response code="200">Detailed health status retrieved successfully</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="503">Service unavailable</response>
    [HttpGet("/api/admin/health")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> DetailedHealth()
    {
        var health = await _healthCheckService.GetHealthStatusAsync();
        return Ok(health);
    }
}
