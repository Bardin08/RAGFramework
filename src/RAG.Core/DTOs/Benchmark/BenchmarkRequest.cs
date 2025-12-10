using System.ComponentModel.DataAnnotations;

namespace RAG.Core.DTOs.Benchmark;

/// <summary>
/// Request to initiate a benchmark run.
/// </summary>
public class BenchmarkRequest
{
    /// <summary>
    /// Name of the dataset to benchmark against (e.g., "dev-seed", "test-seed", "benchmark").
    /// </summary>
    [Required(ErrorMessage = "Dataset name is required")]
    public string Dataset { get; set; } = string.Empty;

    /// <summary>
    /// Optional configuration for the benchmark run.
    /// If not provided, defaults will be used.
    /// </summary>
    public BenchmarkConfiguration? Configuration { get; set; }

    /// <summary>
    /// Optional limit on the number of samples to evaluate.
    /// If not specified, all samples in the dataset will be used.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Sample size must be at least 1")]
    public int? SampleSize { get; set; }

    /// <summary>
    /// Optional list of specific metrics to run.
    /// If not specified, all available metrics will be executed.
    /// </summary>
    public List<string>? Metrics { get; set; }
}
