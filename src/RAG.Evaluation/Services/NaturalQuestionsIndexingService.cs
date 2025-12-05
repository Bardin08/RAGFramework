using System.Text;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Evaluation.Datasets;

namespace RAG.Evaluation.Services;

/// <summary>
/// Service for indexing Natural Questions dataset documents into the RAG system.
/// </summary>
public class NaturalQuestionsIndexingService
{
    private readonly IDocumentIndexingService _documentIndexingService;
    private readonly IDocumentStorageService _storageService;
    private readonly NaturalQuestionsParser _parser;
    private readonly HtmlToTextConverter _htmlConverter;
    private readonly ILogger<NaturalQuestionsIndexingService> _logger;

    public NaturalQuestionsIndexingService(
        IDocumentIndexingService documentIndexingService,
        IDocumentStorageService storageService,
        NaturalQuestionsParser parser,
        HtmlToTextConverter htmlConverter,
        ILogger<NaturalQuestionsIndexingService> logger)
    {
        _documentIndexingService = documentIndexingService ?? throw new ArgumentNullException(nameof(documentIndexingService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _htmlConverter = htmlConverter ?? throw new ArgumentNullException(nameof(htmlConverter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Indexes Natural Questions documents from a JSONL file.
    /// </summary>
    /// <param name="jsonlFilePath">Path to the Natural Questions JSONL file.</param>
    /// <param name="tenantId">The tenant ID to associate with indexed documents.</param>
    /// <param name="ownerId">The owner ID to associate with indexed documents.</param>
    /// <param name="maxDocuments">Maximum number of documents to index (null for all).</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Indexing result summary.</returns>
    public async Task<NaturalQuestionsIndexingResult> IndexDocumentsAsync(
        string jsonlFilePath,
        Guid tenantId,
        Guid ownerId,
        int? maxDocuments = null,
        Action<NaturalQuestionsIndexingProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting Natural Questions indexing from: {FilePath} for tenant {TenantId}",
            jsonlFilePath, tenantId);

        var result = new NaturalQuestionsIndexingResult
        {
            StartTime = DateTimeOffset.UtcNow
        };

        try
        {
            // Parse the JSONL file
            var entries = await _parser.ParseAsync(jsonlFilePath, cancellationToken);
            result.TotalEntries = entries.Count;

            _logger.LogInformation("Parsed {EntryCount} entries from Natural Questions dataset", entries.Count);

            // Limit entries if specified
            var entriesToProcess = maxDocuments.HasValue
                ? entries.Take(maxDocuments.Value).ToList()
                : entries;

            result.TotalEntries = entriesToProcess.Count;

            // Track unique documents by URL to avoid duplicates
            var processedUrls = new HashSet<string>();
            var indexedDocuments = new List<string>();
            var failedDocuments = new List<string>();

            var processedCount = 0;

            foreach (var entry in entriesToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Indexing cancelled by user");
                    result.WasCancelled = true;
                    break;
                }

                // Skip if we've already processed this URL
                if (!string.IsNullOrWhiteSpace(entry.DocumentUrl) &&
                    processedUrls.Contains(entry.DocumentUrl))
                {
                    result.SkippedDuplicates++;
                    continue;
                }

                try
                {
                    // Convert HTML to clean text
                    var cleanText = _htmlConverter.Convert(entry.DocumentHtml);

                    if (string.IsNullOrWhiteSpace(cleanText))
                    {
                        _logger.LogWarning(
                            "Skipping entry {EntryId} - no clean text extracted",
                            entry.Id);
                        result.SkippedEmptyContent++;
                        continue;
                    }

                    // Create unique document ID
                    var documentId = CreateDocumentId(entry);

                    // Save the clean text to storage first
                    var textBytes = Encoding.UTF8.GetBytes(cleanText);
                    using var textStream = new MemoryStream(textBytes);
                    var fileName = $"nq-{entry.Id}.txt";

                    await _storageService.SaveFileAsync(
                        documentId,
                        tenantId,
                        fileName,
                        textStream,
                        cancellationToken);

                    // Index the document
                    await _documentIndexingService.IndexDocumentAsync(
                        documentId: documentId,
                        tenantId: tenantId,
                        ownerId: ownerId,
                        fileName: fileName,
                        title: entry.DocumentTitle,
                        source: entry.DocumentUrl,
                        cancellationToken: cancellationToken);

                    indexedDocuments.Add(documentId.ToString());

                    if (!string.IsNullOrWhiteSpace(entry.DocumentUrl))
                    {
                        processedUrls.Add(entry.DocumentUrl);
                    }

                    result.SuccessfullyIndexed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to index entry {EntryId}: {ErrorMessage}",
                        entry.Id, ex.Message);

                    failedDocuments.Add(entry.Id);
                    result.Failed++;
                }

                processedCount++;

                // Report progress
                if (progressCallback != null && processedCount % 10 == 0)
                {
                    var progress = new NaturalQuestionsIndexingProgress
                    {
                        TotalEntries = result.TotalEntries,
                        ProcessedEntries = processedCount,
                        SuccessfullyIndexed = result.SuccessfullyIndexed,
                        Failed = result.Failed,
                        SkippedDuplicates = result.SkippedDuplicates,
                        SkippedEmptyContent = result.SkippedEmptyContent,
                        PercentComplete = (double)processedCount / result.TotalEntries * 100
                    };

                    progressCallback(progress);
                }
            }

            result.EndTime = DateTimeOffset.UtcNow;
            result.Duration = result.EndTime.Value - result.StartTime;
            result.IndexedDocumentIds = indexedDocuments;
            result.FailedDocumentIds = failedDocuments;

            _logger.LogInformation(
                "Natural Questions indexing completed. " +
                "Total: {Total}, Indexed: {Indexed}, Failed: {Failed}, " +
                "Skipped (duplicates): {SkippedDupes}, Skipped (empty): {SkippedEmpty}, " +
                "Duration: {Duration}s",
                result.TotalEntries,
                result.SuccessfullyIndexed,
                result.Failed,
                result.SkippedDuplicates,
                result.SkippedEmptyContent,
                result.Duration.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTimeOffset.UtcNow;
            result.Duration = result.EndTime.Value - result.StartTime;
            result.Error = ex.Message;

            _logger.LogError(
                ex,
                "Natural Questions indexing failed after {Duration}s",
                result.Duration.TotalSeconds);

            throw;
        }
    }

    /// <summary>
    /// Creates a deterministic GUID from an NQ entry for document ID.
    /// </summary>
    private static Guid CreateDocumentId(NaturalQuestionsEntry entry)
    {
        // Use a combination of entry ID and URL for uniqueness
        var uniqueString = $"nq-{entry.Id}-{entry.DocumentUrl}";
        var bytes = Encoding.UTF8.GetBytes(uniqueString);

        // Create a deterministic GUID using MD5 hash
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(bytes);
        return new Guid(hash);
    }
}

/// <summary>
/// Result of a Natural Questions indexing operation.
/// </summary>
public class NaturalQuestionsIndexingResult
{
    /// <summary>
    /// When the indexing operation started.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// When the indexing operation ended.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Duration of the indexing operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Total number of entries in the dataset.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of documents successfully indexed.
    /// </summary>
    public int SuccessfullyIndexed { get; set; }

    /// <summary>
    /// Number of documents that failed to index.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Number of duplicate documents skipped.
    /// </summary>
    public int SkippedDuplicates { get; set; }

    /// <summary>
    /// Number of documents skipped due to empty content.
    /// </summary>
    public int SkippedEmptyContent { get; set; }

    /// <summary>
    /// Whether the operation was cancelled.
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// IDs of successfully indexed documents.
    /// </summary>
    public List<string> IndexedDocumentIds { get; set; } = new();

    /// <summary>
    /// IDs of documents that failed to index.
    /// </summary>
    public List<string> FailedDocumentIds { get; set; } = new();

    /// <summary>
    /// Whether the indexing was successful overall.
    /// </summary>
    public bool IsSuccess => !WasCancelled && string.IsNullOrEmpty(Error) && Failed == 0;
}

/// <summary>
/// Progress information during Natural Questions indexing.
/// </summary>
public class NaturalQuestionsIndexingProgress
{
    /// <summary>
    /// Total number of entries to process.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of entries processed so far.
    /// </summary>
    public int ProcessedEntries { get; set; }

    /// <summary>
    /// Number of documents successfully indexed so far.
    /// </summary>
    public int SuccessfullyIndexed { get; set; }

    /// <summary>
    /// Number of failures so far.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Number of duplicates skipped so far.
    /// </summary>
    public int SkippedDuplicates { get; set; }

    /// <summary>
    /// Number of empty content entries skipped so far.
    /// </summary>
    public int SkippedEmptyContent { get; set; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete { get; set; }
}
