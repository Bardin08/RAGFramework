using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Authorization handler for document access requirements.
/// Evaluates whether a user has the required permission level for a specific document.
/// </summary>
public class DocumentAccessHandler : AuthorizationHandler<DocumentAccessRequirement, Guid>
{
    private readonly IDocumentPermissionService _permissionService;
    private readonly ILogger<DocumentAccessHandler> _logger;

    public DocumentAccessHandler(
        IDocumentPermissionService permissionService,
        ILogger<DocumentAccessHandler> logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentAccessRequirement requirement,
        Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            _logger.LogDebug("Document access denied: Invalid document ID");
            return;
        }

        var canAccess = await _permissionService.CanAccessAsync(
            documentId,
            context.User,
            requirement.RequiredPermission);

        if (canAccess)
        {
            context.Succeed(requirement);
            _logger.LogDebug(
                "Document access granted: {Permission} for document {DocumentId}",
                requirement.RequiredPermission, documentId);
        }
        else
        {
            _logger.LogDebug(
                "Document access denied: {Permission} for document {DocumentId}",
                requirement.RequiredPermission, documentId);
        }
    }
}
