using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Extensions;
using RAG.API.Models.Requests;
using RAG.API.Models.Responses;
using RAG.Application.Interfaces;
using RAG.Core.Domain.Enums;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for document management operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DocumentsController(IFileUploadService fileUploadService) : ControllerBase
{
    /// <summary>
    /// Upload a document for processing.
    /// </summary>
    /// <param name="request">The document upload request containing the file and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upload response with document ID and status.</returns>
    /// <response code="200">Document already exists (duplicate detected).</response>
    /// <response code="201">Document uploaded successfully.</response>
    /// <response code="400">Invalid request (file type not allowed or file empty).</response>
    /// <response code="401">Unauthorized (missing or invalid JWT token).</response>
    /// <response code="413">File too large (exceeds 10MB limit).</response>
    [HttpPost]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentUploadResponse>> Upload(
        [FromForm] DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await fileUploadService.UploadFileAsync(
            request.File.OpenReadStream(),
            request.File.FileName,
            request.Title,
            request.Source,
            cancellationToken);

        var response = result.ToResponse();

        if (result.Status == DocumentStatus.AlreadyExists)
            return Ok(response);

        return CreatedAtAction(
            nameof(Upload),
            new { id = response.DocumentId },
            response);
    }
}
