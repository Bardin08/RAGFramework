using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Extensions;
using RAG.API.Models.Requests;
using RAG.API.Models.Responses;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.Domain.Enums;
using RAG.Core.Exceptions;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for document management operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DocumentsController(
    IFileUploadService fileUploadService,
    IDocumentRepository documentRepository,
    IDocumentDeletionService documentDeletionService,
    IDocumentIndexingService documentIndexingService,
    ITenantContext tenantContext,
    ILogger<DocumentsController> logger) : ControllerBase
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
    /// <response code="403">Forbidden (admin role required).</response>
    /// <response code="413">File too large (exceeds 10MB limit).</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentUploadResponse>> Upload(
        [FromForm] DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();

        // Step 1: Upload file to storage
        var result = await fileUploadService.UploadFileAsync(
            request.File.OpenReadStream(),
            request.File.FileName,
            request.Title,
            request.Source,
            cancellationToken);

        var response = result.ToResponse();

        if (result.Status == DocumentStatus.AlreadyExists)
        {
            logger.LogInformation(
                "Document {DocumentId} already exists (duplicate detected)",
                result.DocumentId);
            return Ok(response);
        }

        // Step 2: Trigger indexing pipeline asynchronously
        try
        {
            logger.LogInformation(
                "Starting indexing pipeline for document {DocumentId} (file: {FileName})",
                result.DocumentId, request.File.FileName);

            await documentIndexingService.IndexDocumentAsync(
                result.DocumentId,
                tenantId,
                request.File.FileName,
                result.Title,
                request.Source,
                cancellationToken);

            logger.LogInformation(
                "Document {DocumentId} successfully indexed",
                result.DocumentId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to index document {DocumentId}. Document uploaded but not searchable.",
                result.DocumentId);

            // Document is uploaded but not indexed - return success with warning
            // In production, this should trigger a background retry job
        }

        return CreatedAtAction(
            nameof(Upload),
            new { id = response.DocumentId },
            response);
    }

    /// <summary>
    /// Get a paginated list of documents for the current tenant.
    /// </summary>
    /// <param name="query">Query parameters for pagination and filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of documents.</returns>
    /// <response code="200">Returns the list of documents.</response>
    /// <response code="401">Unauthorized (missing or invalid JWT token).</response>
    /// <response code="403">Forbidden (user or admin role required).</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(typeof(PagedResponse<DocumentListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<DocumentListItemResponse>>> GetDocuments(
        [FromQuery] DocumentQueryParameters query,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();

        var (documents, totalCount) = await documentRepository.GetDocumentsAsync(
            tenantId,
            query.Skip,
            query.PageSize,
            query.Search,
            cancellationToken);

        var items = documents.Select(doc => new DocumentListItemResponse
        {
            Id = doc.Id,
            Title = doc.Title,
            Source = doc.Source,
            ChunkCount = doc.ChunkIds.Count,
            IndexedAt = doc.CreatedAt
        }).ToList();

        var response = new PagedResponse<DocumentListItemResponse>
        {
            Items = items,
            Total = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(response);
    }

    /// <summary>
    /// Get detailed information about a specific document.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed document information with chunks.</returns>
    /// <response code="200">Returns the document details.</response>
    /// <response code="401">Unauthorized (missing or invalid JWT token).</response>
    /// <response code="403">Forbidden (user or admin role required).</response>
    /// <response code="404">Document not found or belongs to different tenant.</response>
    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(typeof(DocumentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsResponse>> GetDocument(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();

        var document = await documentRepository.GetDocumentByIdAsync(id, tenantId, cancellationToken);

        if (document == null)
        {
            throw new NotFoundException("Document", id);
        }

        var chunks = await documentRepository.GetDocumentChunksAsync(id, tenantId, cancellationToken);

        var response = new DocumentDetailsResponse
        {
            Id = document.Id,
            Title = document.Title,
            Source = document.Source,
            ChunkCount = document.ChunkIds.Count,
            IndexedAt = document.CreatedAt,
            Metadata = document.Metadata,
            Chunks = chunks.Select(chunk => new DocumentChunkInfo
            {
                Id = chunk.Id,
                ChunkIndex = chunk.ChunkIndex,
                TextPreview = chunk.Text.Length > 200
                    ? chunk.Text.Substring(0, 200) + "..."
                    : chunk.Text,
                StartIndex = chunk.StartIndex,
                EndIndex = chunk.EndIndex
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Delete a document and all its associated data.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="fileName">The original file name (for file storage deletion).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Document deleted successfully.</response>
    /// <response code="401">Unauthorized (missing or invalid JWT token).</response>
    /// <response code="403">Forbidden (admin role required).</response>
    /// <response code="404">Document not found or belongs to different tenant.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        [FromQuery] string fileName,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();

        var deleted = await documentDeletionService.DeleteDocumentAsync(
            id, tenantId, fileName, cancellationToken);

        if (!deleted)
        {
            throw new NotFoundException("Document", id);
        }

        return NoContent();
    }
}
