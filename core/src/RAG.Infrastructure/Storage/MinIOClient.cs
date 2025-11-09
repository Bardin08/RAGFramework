using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;

namespace RAG.Infrastructure.Storage;

/// <summary>
/// MinIO client implementation for object storage operations.
/// </summary>
public class MinIOClient : IMinIOClient
{
    private readonly IMinioClient _minioClient;
    private readonly MinIOSettings _settings;
    private readonly ILogger<MinIOClient> _logger;
    private bool _bucketInitialized;

    public MinIOClient(
        IMinioClient minioClient,
        IOptions<MinIOSettings> settings,
        ILogger<MinIOClient> logger)
    {
        _minioClient = minioClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> UploadDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var objectName = GetObjectName(documentId, tenantId, fileName);
        var contentType = GetContentType(fileName);

        try
        {
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            _logger.LogInformation(
                "Document uploaded to MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);

            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upload document to MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var objectName = GetObjectName(documentId, tenantId, fileName);

        try
        {
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);

            memoryStream.Position = 0;

            _logger.LogInformation(
                "Document downloaded from MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to download document from MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var objectName = GetObjectName(documentId, tenantId, fileName);

        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);

            _logger.LogInformation(
                "Document deleted from MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete document from MinIO: {ObjectName} (DocumentId: {DocumentId}, TenantId: {TenantId})",
                objectName, documentId, tenantId);
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketInitialized)
            return;

        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_settings.BucketName);

            var bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);

            if (!bucketExists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_settings.BucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);

                _logger.LogInformation("Created MinIO bucket: {BucketName}", _settings.BucketName);
            }

            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure MinIO bucket exists: {BucketName}", _settings.BucketName);
            throw;
        }
    }

    private static string GetObjectName(Guid documentId, Guid tenantId, string fileName)
    {
        // Structure: {tenantId}/{documentId}/{fileName}
        return $"{tenantId}/{documentId}/{fileName}";
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".csv" => "text/csv",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }
}
