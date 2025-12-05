namespace RAG.Evaluation.Interfaces;

using RAG.Evaluation.Models;

/// <summary>
/// Interface for exporting evaluation results in various formats.
/// </summary>
public interface IResultsExporter
{
    /// <summary>
    /// The format name (e.g., "CSV", "JSON", "Markdown").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// The MIME content type for this format.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// The file extension for this format (without leading dot).
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Exports a single evaluation report to the specified format.
    /// </summary>
    /// <param name="report">The evaluation report to export.</param>
    /// <param name="options">Optional export options.</param>
    /// <returns>The exported data as a byte array.</returns>
    Task<byte[]> ExportAsync(EvaluationReport report, ExportOptions? options = null);

    /// <summary>
    /// Exports a comparison of multiple evaluation reports.
    /// </summary>
    /// <param name="reports">The evaluation reports to compare.</param>
    /// <param name="options">Optional export options.</param>
    /// <returns>The exported comparison data as a byte array.</returns>
    Task<byte[]> ExportComparisonAsync(List<EvaluationReport> reports, ExportOptions? options = null);
}

/// <summary>
/// Configuration options for results export.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Include detailed per-query breakdown in the export.
    /// </summary>
    public bool IncludePerQueryBreakdown { get; set; } = false;

    /// <summary>
    /// Enable pretty-printing for formats that support it.
    /// </summary>
    public bool PrettyPrint { get; set; } = true;

    /// <summary>
    /// Specific metrics to include. If null, all metrics are included.
    /// </summary>
    public List<string>? MetricsToInclude { get; set; }

    /// <summary>
    /// Include percentile statistics (P50, P95, P99).
    /// </summary>
    public bool IncludePercentiles { get; set; } = true;

    /// <summary>
    /// Include configuration details in the export.
    /// </summary>
    public bool IncludeConfiguration { get; set; } = true;

    /// <summary>
    /// Dataset name for labeling (optional).
    /// </summary>
    public string? DatasetName { get; set; }
}
