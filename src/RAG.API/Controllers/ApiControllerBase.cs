using Microsoft.AspNetCore.Mvc;

namespace RAG.API.Controllers;

/// <summary>
/// Base controller for all versioned API controllers.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
}
