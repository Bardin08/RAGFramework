using Microsoft.Extensions.Logging;
using RAG.Application.Exceptions;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;

namespace RAG.Application.Services;

/// <summary>
/// Service for handling file upload operations with deduplication.
/// </summary>
public class FileUploadService(
    IFileValidationService fileValidationService,
    ITenantContext tenantContext,
    IDocumentStorageService storageService,
    IHashService hashService,
    IDocumentHashRepository documentHashRepository,
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

        fileStream.Position = 0;
        var documentHash = hashService.ComputeHash(fileStream);
        fileStream.Position = 0;

        logger.LogDebug("Computed hash {Hash} for file {FileName}", documentHash, fileName);

        var existingHash = await documentHashRepository.GetByHashAsync(documentHash, tenantId, cancellationToken);
        if (existingHash != null)
        {
            logger.LogInformation(
                "Duplicate document detected. Hash: {Hash}, Original: {OriginalFileName}, " +
                "Duplicate: {DuplicateFileName}, TenantId: {TenantId}, ExistingDocumentId: {DocumentId}",
                documentHash, existingHash.OriginalFileName, fileName, tenantId, existingHash.DocumentId);

            return new FileUploadResult
            {
                DocumentId = existingHash.DocumentId,
                Title = title ?? fileName,
                Status = DocumentStatus.AlreadyExists,
                UploadedAt = DateTime.UtcNow
            };
        }

        var documentId = Guid.NewGuid();
        var storagePath = await storageService.SaveFileAsync(
            documentId, tenantId, fileName, fileStream, cancellationToken);

        var hashRecord = new DocumentHash(
            id: Guid.NewGuid(),
            hash: documentHash,
            documentId: documentId,
            originalFileName: fileName,
            uploadedAt: DateTime.UtcNow,
            uploadedBy: tenantId, // For now, use tenantId as uploadedBy (will be user ID in future)
            tenantId: tenantId);

        var success = await documentHashRepository.TryAddAsync(hashRecord, cancellationToken);

        if (!success)
        {
            logger.LogInformation(
                "Race condition detected for hash {Hash}. Querying for existing document.",
                documentHash);

            var racedHash = await documentHashRepository.GetByHashAsync(
                documentHash, tenantId, cancellationToken);

            if (racedHash != null)
            {
                logger.LogInformation(
                    "Returning existing document ID {DocumentId} after race condition",
                    racedHash.DocumentId);

                return new FileUploadResult
                {
                    DocumentId = racedHash.DocumentId,
                    Title = title ?? fileName,
                    Status = DocumentStatus.AlreadyExists,
                    UploadedAt = DateTime.UtcNow
                };
            }

            logger.LogError("Failed to resolve race condition for hash {Hash}", documentHash);
            throw new InvalidOperationException($"Failed to resolve race condition for document hash {documentHash}");
        }

        logger.LogInformation(
            "Document {DocumentId} uploaded successfully by tenant {TenantId}. " +
            "Hash: {Hash}, Storage path: {StoragePath}",
            documentId, tenantId, documentHash, storagePath);

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
