namespace RAG.Evaluation.Export;

using RAG.Evaluation.Interfaces;

/// <summary>
/// Factory for creating results exporters based on format.
/// </summary>
public class ResultsExporterFactory
{
    private readonly Dictionary<string, Func<IResultsExporter>> _exporters;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultsExporterFactory"/> class.
    /// </summary>
    public ResultsExporterFactory()
    {
        _exporters = new Dictionary<string, Func<IResultsExporter>>(StringComparer.OrdinalIgnoreCase)
        {
            ["csv"] = () => new CsvResultsExporter(),
            ["json"] = () => new JsonResultsExporter(),
            ["md"] = () => new MarkdownResultsExporter(),
            ["markdown"] = () => new MarkdownResultsExporter()
        };
    }

    /// <summary>
    /// Gets an exporter for the specified format.
    /// </summary>
    /// <param name="format">The export format (csv, json, md, markdown).</param>
    /// <returns>An instance of <see cref="IResultsExporter"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the format is not supported.</exception>
    public IResultsExporter GetExporter(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("Format cannot be null or empty.", nameof(format));
        }

        format = format.Trim().ToLowerInvariant();

        if (!_exporters.TryGetValue(format, out var factory))
        {
            var supportedFormats = string.Join(", ", _exporters.Keys);
            throw new ArgumentException(
                $"Unknown export format '{format}'. Supported formats: {supportedFormats}",
                nameof(format));
        }

        return factory();
    }

    /// <summary>
    /// Gets all supported export formats.
    /// </summary>
    /// <returns>A collection of supported format names.</returns>
    public IReadOnlyCollection<string> GetSupportedFormats()
    {
        return _exporters.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Checks if a format is supported.
    /// </summary>
    /// <param name="format">The format to check.</param>
    /// <returns>True if the format is supported; otherwise, false.</returns>
    public bool IsFormatSupported(string format)
    {
        return !string.IsNullOrWhiteSpace(format) &&
               _exporters.ContainsKey(format.Trim().ToLowerInvariant());
    }
}
