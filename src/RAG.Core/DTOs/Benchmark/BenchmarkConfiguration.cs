using System.ComponentModel.DataAnnotations;

namespace RAG.Core.DTOs.Benchmark;

/// <summary>
/// Configuration options for a benchmark run.
/// </summary>
public class BenchmarkConfiguration
{
    /// <summary>
    /// Retrieval strategy to benchmark (BM25, Dense, Hybrid, Adaptive).
    /// </summary>
    public string? RetrievalStrategy { get; set; }

    /// <summary>
    /// Number of top results to retrieve (topK).
    /// </summary>
    [Range(1, 100)]
    public int? TopK { get; set; }

    /// <summary>
    /// Optional LLM provider to test (OpenAI, Ollama).
    /// </summary>
    public string? LlmProvider { get; set; }

    /// <summary>
    /// Optional tenant ID to scope the benchmark.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Additional configuration parameters as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}
