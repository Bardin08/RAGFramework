using Microsoft.Extensions.Logging;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Service for processing TriviaQA dataset files and preparing them for evaluation.
/// </summary>
public class TriviaQAProcessingService
{
    private readonly TriviaQAParser _parser;
    private readonly ILogger<TriviaQAProcessingService> _logger;

    public TriviaQAProcessingService(
        TriviaQAParser parser,
        ILogger<TriviaQAProcessingService> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Processes a TriviaQA raw file and generates processed documents and ground truth.
    /// </summary>
    /// <param name="rawFilePath">Path to raw TriviaQA JSON file.</param>
    /// <param name="outputBaseDirectory">Base output directory (e.g., data/benchmarks/triviaqa).</param>
    /// <param name="datasetName">Name for the dataset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with statistics.</returns>
    public async Task<TriviaQAProcessingResult> ProcessDatasetAsync(
        string rawFilePath,
        string outputBaseDirectory,
        string datasetName = "TriviaQA",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing TriviaQA dataset from {RawFile} to {OutputDir}",
            rawFilePath, outputBaseDirectory);

        var result = new TriviaQAProcessingResult
        {
            RawFilePath = rawFilePath,
            OutputDirectory = outputBaseDirectory,
            DatasetName = datasetName,
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            // Parse the TriviaQA file
            var entries = await _parser.ParseAsync(rawFilePath, cancellationToken);
            result.TotalQuestions = entries.Count;

            if (entries.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No entries found in TriviaQA file";
                return result;
            }

            // Extract documents
            var documents = _parser.ExtractDocuments(entries);
            result.TotalDocuments = documents.Count;

            // Convert to ground truth
            var groundTruth = _parser.ConvertToGroundTruth(entries, datasetName);
            result.ValidGroundTruthEntries = groundTruth.Entries.Count;
            result.ValidationErrors = groundTruth.ValidationErrors.Count;

            // Set up output paths
            var documentsDir = Path.Combine(outputBaseDirectory, "processed", "documents");
            var groundTruthPath = Path.Combine(outputBaseDirectory, "processed", "ground-truth.json");

            // Save documents
            await _parser.SaveDocumentsAsync(documents, documentsDir, cancellationToken);
            result.DocumentsOutputPath = documentsDir;

            // Save ground truth
            await _parser.SaveGroundTruthAsync(groundTruth, groundTruthPath, cancellationToken);
            result.GroundTruthOutputPath = groundTruthPath;

            result.Success = true;
            result.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Successfully processed TriviaQA dataset: {Questions} questions, {Documents} documents, {GroundTruth} ground truth entries, {Errors} errors",
                result.TotalQuestions, result.TotalDocuments, result.ValidGroundTruthEntries, result.ValidationErrors);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogError(ex, "Failed to process TriviaQA dataset from {RawFile}", rawFilePath);

            throw;
        }
    }

    /// <summary>
    /// Loads processed documents from the output directory.
    /// </summary>
    /// <param name="documentsDirectory">Directory containing processed documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of loaded documents.</returns>
    public async Task<List<TriviaQADocument>> LoadProcessedDocumentsAsync(
        string documentsDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading processed documents from {Directory}", documentsDirectory);

        if (!Directory.Exists(documentsDirectory))
        {
            _logger.LogWarning("Documents directory does not exist: {Directory}", documentsDirectory);
            return new List<TriviaQADocument>();
        }

        var documentFiles = Directory.GetFiles(documentsDirectory, "*.json");
        var documents = new List<TriviaQADocument>();

        foreach (var file in documentFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var doc = System.Text.Json.JsonSerializer.Deserialize<TriviaQADocument>(json);

                if (doc != null)
                {
                    documents.Add(doc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load document from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} documents from {Directory}", documents.Count, documentsDirectory);
        return documents;
    }
}

/// <summary>
/// Result of TriviaQA dataset processing.
/// </summary>
public class TriviaQAProcessingResult
{
    /// <summary>
    /// Path to the raw input file.
    /// </summary>
    public string RawFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Output base directory.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Dataset name.
    /// </summary>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>
    /// Whether processing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total number of questions in the dataset.
    /// </summary>
    public int TotalQuestions { get; set; }

    /// <summary>
    /// Total number of extracted documents.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of valid ground truth entries.
    /// </summary>
    public int ValidGroundTruthEntries { get; set; }

    /// <summary>
    /// Number of validation errors.
    /// </summary>
    public int ValidationErrors { get; set; }

    /// <summary>
    /// Path where documents were saved.
    /// </summary>
    public string? DocumentsOutputPath { get; set; }

    /// <summary>
    /// Path where ground truth was saved.
    /// </summary>
    public string? GroundTruthOutputPath { get; set; }

    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When processing completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Processing duration.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : null;
}
