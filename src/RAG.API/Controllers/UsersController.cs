using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Models.Responses;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for user lookup operations (for sharing autocomplete).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")] // Backward compatibility
[Authorize]
public class UsersController(
    IUserLookupService userLookupService,
    ITenantContext tenantContext,
    ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>
    /// Search for users within the current tenant for sharing autocomplete.
    /// </summary>
    /// <param name="search">Search term (email or username prefix).</param>
    /// <param name="limit">Maximum number of results (default 10, max 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching users.</returns>
    /// <response code="200">Returns matching users.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserDto>>> SearchUsers(
        [FromQuery] string? search,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
        {
            return Ok(new List<UserDto>());
        }

        var tenantId = tenantContext.GetTenantId();
        var maxResults = Math.Min(limit, 50);

        logger.LogDebug(
            "User search requested: term='{SearchTerm}', tenant={TenantId}, limit={Limit}",
            search, tenantId, maxResults);

        var users = await userLookupService.SearchUsersAsync(
            tenantId, search, maxResults, cancellationToken);

        var result = users.Select(u => new UserDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Username = u.Username
        }).ToList();

        return Ok(result);
    }
}
