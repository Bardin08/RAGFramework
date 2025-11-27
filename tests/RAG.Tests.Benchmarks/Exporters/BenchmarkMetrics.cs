namespace RAG.Tests.Benchmarks.Exporters;

/// <summary>
/// Represents aggregated metrics for benchmark results.
/// </summary>
public record BenchmarkMetrics
{
    public required double Precision5 { get; init; }
    public required double Precision10 { get; init; }
    public required double Recall5 { get; init; }
    public required double Recall10 { get; init; }
    public required double MRR { get; init; }
    public required double P50Ms { get; init; }
    public required double P95Ms { get; init; }
    public required double P99Ms { get; init; }
    public required int QueryCount { get; init; }
}
