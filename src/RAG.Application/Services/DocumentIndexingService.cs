using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using System.Diagnostics;

namespace RAG.Application.Services;

/// <summary>
/// Orchestrates the full document indexing pipeline: extract → clean → chunk → embed → index.
/// </summary>
public class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IDocumentStorageService _storageService;
    private readonly ITextExtractor _textExtractor;
    private readonly ITextCleanerService _textCleaner;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchEngineClient _searchEngineClient;
    private readonly IVectorStoreClient _vectorStoreClient;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocumentIndexingService> _logger;

    private const int EmbeddingBatchSize = 32;

    public DocumentIndexingService(
        IDocumentStorageService storageService,
        ITextExtractor textExtractor,
        ITextCleanerService textCleaner,
        IChunkingStrategy chunkingStrategy,
        IEmbeddingService embeddingService,
        ISearchEngineClient searchEngineClient,
        IVectorStoreClient vectorStoreClient,
        IDocumentRepository documentRepository,
        ILogger<DocumentIndexingService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _textCleaner = textCleaner ?? throw new ArgumentNullException(nameof(textCleaner));
        _chunkingStrategy = chunkingStrategy ?? throw new ArgumentNullException(nameof(chunkingStrategy));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _searchEngineClient = searchEngineClient ?? throw new ArgumentNullException(nameof(searchEngineClient));
        _vectorStoreClient = vectorStoreClient ?? throw new ArgumentNullException(nameof(vectorStoreClient));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task IndexDocumentAsync(
        Guid documentId,
        Guid tenantId,
        Guid ownerId,
        string fileName,
        string title,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting indexing pipeline for document {DocumentId} (tenant {TenantId}, owner {OwnerId}, file {FileName})",
            documentId, tenantId, ownerId, fileName);

        List<DocumentChunk>? chunks = null;
        bool elasticsearchIndexed = false;
        bool qdrantIndexed = false;
        bool databaseSaved = false;

        try
        {
            // Step 1: Extract text from document
            var text = await ExtractTextAsync(documentId, tenantId, fileName, cancellationToken);

            // Step 2: Clean and normalize text
            text = CleanText(text, documentId);

            // Step 3: Chunk text into fragments
            chunks = await ChunkTextAsync(text, documentId, tenantId, cancellationToken);

            // Step 4: Generate embeddings for chunks
            var embeddings = await GenerateEmbeddingsAsync(chunks, cancellationToken);

            // Step 5: Save Document and Chunks to PostgreSQL
            databaseSaved = await SaveToDatabase(
                documentId, tenantId, ownerId, title, source ?? fileName, text, chunks, cancellationToken);

            // Step 6: Index chunks in Elasticsearch (with error handling)
            elasticsearchIndexed = await IndexInElasticsearchAsync(chunks, cancellationToken);

            // Step 7: Index embeddings in Qdrant (with error handling)
            qdrantIndexed = await IndexInQdrantAsync(chunks, embeddings, tenantId, source ?? fileName, cancellationToken);

            // Check if at least database save succeeded
            if (!databaseSaved)
            {
                throw new InvalidOperationException(
                    "Failed to save document to database");
            }

            overallStopwatch.Stop();

            _logger.LogInformation(
                "Successfully completed indexing pipeline for document {DocumentId}. " +
                "Total time: {ElapsedMs}ms, Chunks: {ChunkCount}, Database: {DatabaseStatus}, " +
                "Elasticsearch: {ElasticsearchStatus}, Qdrant: {QdrantStatus}",
                documentId, overallStopwatch.ElapsedMilliseconds, chunks.Count,
                databaseSaved ? "Success" : "Failed",
                elasticsearchIndexed ? "Success" : "Failed",
                qdrantIndexed ? "Success" : "Failed");
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();

            _logger.LogError(
                ex,
                "Indexing pipeline failed for document {DocumentId} after {ElapsedMs}ms. " +
                "Elasticsearch indexed: {ElasticsearchIndexed}, Qdrant indexed: {QdrantIndexed}",
                documentId, overallStopwatch.ElapsedMilliseconds, elasticsearchIndexed, qdrantIndexed);

            // Attempt cleanup if partial indexing occurred
            if (chunks != null && (elasticsearchIndexed || qdrantIndexed))
            {
                await CleanupPartialIndexingAsync(
                    documentId, tenantId, chunks, elasticsearchIndexed, qdrantIndexed, cancellationToken);
            }

            throw;
        }
    }

    /// <summary>
    /// Step 1: Extract text from document.
    /// </summary>
    private async Task<string> ExtractTextAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Extracting text from document {DocumentId}", documentId);

        using var documentStream = await _storageService.GetFileAsync(
            documentId, tenantId, cancellationToken);

        if (documentStream == null)
        {
            throw new InvalidOperationException($"Document {documentId} not found in storage");
        }

        var text = await _textExtractor.ExtractTextAsync(
            documentStream, fileName, cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Text extraction completed for document {DocumentId}. " +
            "Extracted {CharCount} characters in {ElapsedMs}ms",
            documentId, text.Length, stopwatch.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"No text extracted from document {documentId}");
        }

        return text;
    }

    /// <summary>
    /// Step 2: Clean and normalize extracted text.
    /// </summary>
    private string CleanText(string text, Guid documentId)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Cleaning text for document {DocumentId}", documentId);

        var cleanedText = _textCleaner.CleanText(text);

        stopwatch.Stop();

        _logger.LogInformation(
            "Text cleaning completed for document {DocumentId}. " +
            "Original: {OriginalLength} chars -> Cleaned: {CleanedLength} chars " +
            "({ReductionPercent:F1}% reduction) in {ElapsedMs}ms",
            documentId,
            text.Length,
            cleanedText.Length,
            (1 - (double)cleanedText.Length / text.Length) * 100,
            stopwatch.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            throw new InvalidOperationException(
                $"Text cleaning resulted in empty content for document {documentId}");
        }

        return cleanedText;
    }

    /// <summary>
    /// Step 3: Chunk text into fragments.
    /// </summary>
    private async Task<List<DocumentChunk>> ChunkTextAsync(
        string text,
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Chunking text for document {DocumentId}", documentId);

        var chunks = await _chunkingStrategy.ChunkAsync(
            text, documentId, tenantId, cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Chunking completed for document {DocumentId}. " +
            "Created {ChunkCount} chunks in {ElapsedMs}ms",
            documentId, chunks.Count, stopwatch.ElapsedMilliseconds);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException($"No chunks created from document {documentId}");
        }

        return chunks;
    }

    /// <summary>
    /// Step 4: Generate embeddings for chunks in batches.
    /// </summary>
    private async Task<List<float[]>> GenerateEmbeddingsAsync(
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Generating embeddings for {ChunkCount} chunks in batches of {BatchSize}",
            chunks.Count, EmbeddingBatchSize);

        var allEmbeddings = new List<float[]>();
        var batchCount = (int)Math.Ceiling(chunks.Count / (double)EmbeddingBatchSize);

        for (int i = 0; i < batchCount; i++)
        {
            var batchChunks = chunks
                .Skip(i * EmbeddingBatchSize)
                .Take(EmbeddingBatchSize)
                .ToList();

            var texts = batchChunks.Select(c => c.Text).ToList();

            _logger.LogDebug(
                "Processing embedding batch {BatchNumber}/{TotalBatches} ({ChunkCount} chunks)",
                i + 1, batchCount, batchChunks.Count);

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                texts, cancellationToken);

            allEmbeddings.AddRange(embeddings);
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Embedding generation completed. Generated {EmbeddingCount} embeddings " +
            "in {BatchCount} batches, total time: {ElapsedMs}ms",
            allEmbeddings.Count, batchCount, stopwatch.ElapsedMilliseconds);

        if (allEmbeddings.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count mismatch: expected {chunks.Count}, got {allEmbeddings.Count}");
        }

        return allEmbeddings;
    }

    /// <summary>
    /// Step 6: Index chunks in Elasticsearch.
    /// </summary>
    private async Task<bool> IndexInElasticsearchAsync(
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Indexing {ChunkCount} chunks in Elasticsearch", chunks.Count);

        try
        {
            await _searchEngineClient.BulkIndexAsync(chunks, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Elasticsearch indexing completed. Indexed {ChunkCount} chunks in {ElapsedMs}ms",
                chunks.Count, stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Elasticsearch indexing failed after {ElapsedMs}ms. Will attempt Qdrant indexing.",
                stopwatch.ElapsedMilliseconds);

            return false;
        }
    }

    /// <summary>
    /// Step 7: Index embeddings in Qdrant.
    /// </summary>
    private async Task<bool> IndexInQdrantAsync(
        List<DocumentChunk> chunks,
        List<float[]> embeddings,
        Guid tenantId,
        string source,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Indexing {EmbeddingCount} embeddings in Qdrant", embeddings.Count);

        try
        {
            var vectors = chunks.Zip(embeddings, (chunk, embedding) => (
                Id: chunk.Id,
                Embedding: embedding,
                Payload: CreateQdrantPayload(chunk, tenantId, source)
            )).ToList();

            await _vectorStoreClient.BatchUpsertAsync(vectors, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Qdrant indexing completed. Indexed {VectorCount} vectors in {ElapsedMs}ms",
                vectors.Count, stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Qdrant indexing failed after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            return false;
        }
    }

    /// <summary>
    /// Step 5: Save Document and Chunks to PostgreSQL.
    /// </summary>
    private async Task<bool> SaveToDatabase(
        Guid documentId,
        Guid tenantId,
        Guid ownerId,
        string title,
        string source,
        string content,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Saving document {DocumentId} and {ChunkCount} chunks to database", documentId, chunks.Count);

        try
        {
            // Create Document entity
            var chunkIds = chunks.Select(c => c.Id).ToList();
            var document = new Document(
                id: documentId,
                title: title,
                content: content,
                tenantId: tenantId,
                ownerId: ownerId,
                source: source,
                metadata: new Dictionary<string, object>
                {
                    ["fileType"] = Path.GetExtension(source),
                    ["characterCount"] = content.Length,
                    ["chunkCount"] = chunks.Count
                },
                chunkIds: chunkIds);

            // Save to database
            await _documentRepository.AddDocumentWithChunksAsync(document, chunks, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Database save completed for document {DocumentId}. " +
                "Saved 1 document and {ChunkCount} chunks in {ElapsedMs}ms",
                documentId, chunks.Count, stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Database save failed after {ElapsedMs}ms for document {DocumentId}",
                stopwatch.ElapsedMilliseconds, documentId);

            return false;
        }
    }

    /// <summary>
    /// Creates payload for Qdrant vector storage.
    /// </summary>
    private static Dictionary<string, object> CreateQdrantPayload(DocumentChunk chunk, Guid tenantId, string source)
    {
        var payload = new Dictionary<string, object>
        {
            ["documentId"] = chunk.DocumentId,
            ["tenantId"] = tenantId,
            ["text"] = chunk.Text,
            ["source"] = source,
            ["startIndex"] = chunk.StartIndex,
            ["endIndex"] = chunk.EndIndex,
            ["chunkIndex"] = chunk.ChunkIndex
        };

        // Add chunk metadata to payload
        foreach (var kvp in chunk.Metadata)
        {
            payload[$"metadata_{kvp.Key}"] = kvp.Value;
        }

        return payload;
    }

    /// <summary>
    /// Attempts to clean up partially indexed data.
    /// </summary>
    private async Task CleanupPartialIndexingAsync(
        Guid documentId,
        Guid tenantId,
        List<DocumentChunk> chunks,
        bool elasticsearchIndexed,
        bool qdrantIndexed,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Attempting cleanup for document {DocumentId} (Elasticsearch: {ElasticsearchIndexed}, Qdrant: {QdrantIndexed})",
            documentId, elasticsearchIndexed, qdrantIndexed);

        try
        {
            if (elasticsearchIndexed)
            {
                _logger.LogDebug("Cleaning up Elasticsearch chunks for document {DocumentId}", documentId);
                await _searchEngineClient.DeleteDocumentChunksAsync(documentId, tenantId, cancellationToken);
            }

            if (qdrantIndexed)
            {
                _logger.LogDebug("Cleaning up Qdrant vectors for document {DocumentId}", documentId);
                await _vectorStoreClient.DeleteDocumentVectorsAsync(documentId, tenantId, cancellationToken);
            }

            _logger.LogInformation("Cleanup completed for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed for document {DocumentId}", documentId);
            // Don't throw - cleanup is best-effort
        }
    }
}
