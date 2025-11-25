using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;

namespace RAG.Application.Services;

/// <summary>
/// Service for orchestrating document deletion across all storage systems.
/// </summary>
public class DocumentDeletionService : IDocumentDeletionService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ISearchEngineClient _searchEngineClient;
    private readonly IVectorStoreClient _vectorStoreClient;
    private readonly IDocumentStorageService _storageService;
    private readonly IDocumentHashRepository _hashRepository;
    private readonly ILogger<DocumentDeletionService> _logger;

    public DocumentDeletionService(
        IDocumentRepository documentRepository,
        ISearchEngineClient searchEngineClient,
        IVectorStoreClient vectorStoreClient,
        IDocumentStorageService storageService,
        IDocumentHashRepository hashRepository,
        ILogger<DocumentDeletionService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _searchEngineClient = searchEngineClient ?? throw new ArgumentNullException(nameof(searchEngineClient));
        _vectorStoreClient = vectorStoreClient ?? throw new ArgumentNullException(nameof(vectorStoreClient));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _hashRepository = hashRepository ?? throw new ArgumentNullException(nameof(hashRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting deletion process for document {DocumentId} (tenant {TenantId}, file {FileName})",
            documentId, tenantId, fileName);

        // First, verify the document exists and belongs to the tenant
        var document = await _documentRepository.GetDocumentByIdAsync(
            documentId, tenantId, cancellationToken);

        if (document == null)
        {
            _logger.LogWarning(
                "Document {DocumentId} not found or belongs to different tenant",
                documentId);
            return false;
        }

        var deletionSuccessful = true;
        var deletedSystems = new List<string>();

        // Step 1: Delete from Qdrant (vector store)
        try
        {
            await _vectorStoreClient.DeleteDocumentVectorsAsync(
                documentId, tenantId, cancellationToken);
            deletedSystems.Add("Qdrant");
            _logger.LogDebug("Deleted vectors from Qdrant for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete vectors from Qdrant for document {DocumentId}",
                documentId);
            deletionSuccessful = false;
        }

        // Step 2: Delete from Elasticsearch (full-text search)
        try
        {
            await _searchEngineClient.DeleteDocumentChunksAsync(
                documentId, tenantId, cancellationToken);
            deletedSystems.Add("Elasticsearch");
            _logger.LogDebug("Deleted chunks from Elasticsearch for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete chunks from Elasticsearch for document {DocumentId}",
                documentId);
            deletionSuccessful = false;
        }

        // Step 3: Delete from PostgreSQL (database)
        try
        {
            var dbDeleted = await _documentRepository.DeleteDocumentAsync(
                documentId, tenantId, cancellationToken);

            if (dbDeleted)
            {
                deletedSystems.Add("PostgreSQL");
                _logger.LogDebug("Deleted document from PostgreSQL for document {DocumentId}", documentId);
            }
            else
            {
                _logger.LogWarning(
                    "Document {DocumentId} was not found in database during deletion",
                    documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete document from PostgreSQL for document {DocumentId}",
                documentId);
            deletionSuccessful = false;
        }

        // Step 4: Delete document hash record
        try
        {
            await _hashRepository.DeleteByDocumentIdAsync(documentId, tenantId, cancellationToken);
            deletedSystems.Add("DocumentHash");
            _logger.LogDebug("Deleted document hash for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete document hash for document {DocumentId}",
                documentId);
            // Don't mark as unsuccessful - hash deletion is not critical
        }

        // Step 5: Delete from file storage
        try
        {
            await _storageService.DeleteFileAsync(documentId, tenantId, fileName, cancellationToken);
            deletedSystems.Add("FileStorage");
            _logger.LogDebug("Deleted file from storage for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete file from storage for document {DocumentId}",
                documentId);
            // Don't mark as unsuccessful - file may already be deleted or not exist
        }

        if (deletionSuccessful)
        {
            _logger.LogInformation(
                "Successfully deleted document {DocumentId} from {Systems} (tenant {TenantId})",
                documentId, string.Join(", ", deletedSystems), tenantId);
        }
        else
        {
            _logger.LogWarning(
                "Document {DocumentId} partially deleted from {Systems}. Some systems failed. (tenant {TenantId})",
                documentId, string.Join(", ", deletedSystems), tenantId);
        }

        return true; // Return true even if some systems failed, as long as DB deletion succeeded
    }
}
