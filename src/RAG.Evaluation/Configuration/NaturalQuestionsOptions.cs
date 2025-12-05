namespace RAG.Evaluation.Configuration;

/// <summary>
/// Configuration options for Natural Questions dataset evaluation.
/// </summary>
public class NaturalQuestionsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "NaturalQuestions";

    /// <summary>
    /// Path to the Natural Questions JSONL dataset file.
    /// </summary>
    public string DatasetPath { get; set; } = "./data/benchmarks/natural-questions/nq-train.jsonl";

    /// <summary>
    /// Directory to store processed documents.
    /// </summary>
    public string DocumentsDirectory { get; set; } = "./data/benchmarks/natural-questions/documents";

    /// <summary>
    /// Path to the generated ground truth file.
    /// </summary>
    public string GroundTruthPath { get; set; } = "./data/benchmarks/natural-questions/ground-truth.json";

    /// <summary>
    /// Whether to include only entries with short answers.
    /// Short answers are typically better for evaluation.
    /// </summary>
    public bool IncludeOnlyWithShortAnswers { get; set; } = true;

    /// <summary>
    /// Maximum number of entries to process from the dataset.
    /// Null means process all entries.
    /// </summary>
    public int? MaxEntries { get; set; } = 100;

    /// <summary>
    /// Maximum number of documents to index.
    /// Null means index all unique documents.
    /// </summary>
    public int? MaxDocumentsToIndex { get; set; } = 50;

    /// <summary>
    /// Whether to skip duplicate documents (same Wikipedia URL).
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;

    /// <summary>
    /// Minimum content length (in characters) for a document to be indexed.
    /// Documents with less content will be skipped.
    /// </summary>
    public int MinContentLength { get; set; } = 100;

    /// <summary>
    /// Batch size for progress reporting during indexing.
    /// </summary>
    public int ProgressReportingBatchSize { get; set; } = 10;

    /// <summary>
    /// Tenant ID to use for indexing Natural Questions documents.
    /// If not specified, a default benchmark tenant will be used.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Owner ID to use for indexing Natural Questions documents.
    /// If not specified, a default benchmark owner will be used.
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Whether to regenerate ground truth even if it already exists.
    /// </summary>
    public bool RegenerateGroundTruth { get; set; } = false;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DatasetPath))
            errors.Add("DatasetPath is required");

        if (string.IsNullOrWhiteSpace(DocumentsDirectory))
            errors.Add("DocumentsDirectory is required");

        if (string.IsNullOrWhiteSpace(GroundTruthPath))
            errors.Add("GroundTruthPath is required");

        if (MaxEntries.HasValue && MaxEntries.Value <= 0)
            errors.Add("MaxEntries must be greater than 0");

        if (MaxDocumentsToIndex.HasValue && MaxDocumentsToIndex.Value <= 0)
            errors.Add("MaxDocumentsToIndex must be greater than 0");

        if (MinContentLength < 0)
            errors.Add("MinContentLength cannot be negative");

        if (ProgressReportingBatchSize <= 0)
            errors.Add("ProgressReportingBatchSize must be greater than 0");

        return errors.Count == 0;
    }
}
