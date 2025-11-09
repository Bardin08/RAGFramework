using Microsoft.Extensions.Logging;
using RAG.Application.Exceptions;
using RAG.Application.Interfaces;
using RAG.Core.Domain.Enums;

namespace RAG.Application.Services;

/// <summary>
/// Service for handling file upload operations.
/// </summary>
public class FileUploadService(
    IFileValidationService fileValidationService,
    ITenantContext tenantContext,
    IDocumentStorageService storageService,
    ILogger<FileUploadService> logger)
    : IFileUploadService
{
    /// <inheritdoc />
    public async Task<FileUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string? title,
        string? source,
        CancellationToken cancellationToken = default)
    {
        var validationResult = fileValidationService.ValidateFile(fileStream, fileName);
        if (!validationResult.IsValid)
            HandleFileValidationError(fileName, validationResult);

        var tenantId = GetTenantId();
        var documentId = Guid.NewGuid();
        fileStream.Position = 0;

        var storagePath = await storageService.SaveFileAsync(
            documentId, tenantId, fileName, fileStream, cancellationToken);

        logger.LogInformation(
            "Document {DocumentId} uploaded successfully by tenant {TenantId}. Storage path: {StoragePath}",
            documentId, tenantId, storagePath);

        return new FileUploadResult
        {
            DocumentId = documentId,
            Title = title ?? fileName,
            Status = DocumentStatus.Uploaded,
            UploadedAt = DateTime.UtcNow
        };
    }

    private void HandleFileValidationError(string fileName, FileValidationResult validationResult)
    {
        logger.LogWarning("File validation failed for {FileName}: {Errors}",
            fileName, string.Join(", ", validationResult.Errors));

        var isSizeError = validationResult.Errors.Any(e => e.Contains("exceeds maximum"));
        if (isSizeError)
        {
            throw new FileSizeException(string.Join("; ", validationResult.Errors));
        }

        throw new FileValidationException(validationResult.Errors);
    }

    private Guid GetTenantId()
    {
        Guid tenantId;
        try
        {
            tenantId = tenantContext.GetCurrentTenantId();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to extract tenant ID from JWT token");
            throw new TenantException("Missing or invalid tenant information", ex);
        }

        return tenantId;
    }
}