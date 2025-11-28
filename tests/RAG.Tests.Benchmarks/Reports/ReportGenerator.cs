using RAG.Tests.Benchmarks.Exporters;

namespace RAG.Tests.Benchmarks.Reports;

/// <summary>
/// Generates formatted console reports for benchmark results.
/// </summary>
public static class ReportGenerator
{
    /// <summary>
    /// Generates and prints a console report comparing BM25 and Dense retrieval.
    /// </summary>
    /// <param name="metrics">Dictionary of metrics keyed by "Strategy|QueryType".</param>
    public static void GenerateConsoleReport(Dictionary<string, BenchmarkMetrics> metrics)
    {
        Console.WriteLine();
        Console.WriteLine("====================================================================");
        Console.WriteLine("                Benchmark Results: BM25 vs Dense Retrieval");
        Console.WriteLine("====================================================================");
        Console.WriteLine();

        // Overall performance comparison
        if (metrics.TryGetValue("BM25|Overall", out var bm25Overall) &&
            metrics.TryGetValue("Dense|Overall", out var denseOverall))
        {
            Console.WriteLine("Overall Performance:");
            Console.WriteLine("--------------------------------------------------------------------");
            Console.WriteLine($"  Strategy   │ Precision@5 │ Recall@5 │   MRR   │  P95 Latency");
            Console.WriteLine("─────────────┼─────────────┼──────────┼─────────┼──────────────");
            Console.WriteLine($"  BM25       │    {bm25Overall.Precision5:0.000}    │  {bm25Overall.Recall5:0.000}   │ {bm25Overall.MRR:0.000}   │   {bm25Overall.P95Ms:0.0} ms");
            Console.WriteLine($"  Dense      │    {denseOverall.Precision5:0.000}    │  {denseOverall.Recall5:0.000}   │ {denseOverall.MRR:0.000}   │   {denseOverall.P95Ms:0.0} ms");
            Console.WriteLine();

            // Trade-offs analysis
            Console.WriteLine("Trade-offs:");
            Console.WriteLine("--------------------------------------------------------------------");

            var bm25Faster = bm25Overall.P95Ms < denseOverall.P95Ms;
            var denseMoreAccurate = denseOverall.Precision5 > bm25Overall.Precision5;

            if (bm25Faster && denseMoreAccurate)
            {
                var speedDiff = ((denseOverall.P95Ms - bm25Overall.P95Ms) / bm25Overall.P95Ms) * 100;
                var accuracyDiff = ((denseOverall.Precision5 - bm25Overall.Precision5) / bm25Overall.Precision5) * 100;

                Console.WriteLine($"  • BM25:  {speedDiff:0.0}% faster ({bm25Overall.P95Ms:0.0}ms vs {denseOverall.P95Ms:0.0}ms)");
                Console.WriteLine($"           Lower precision ({bm25Overall.Precision5:0.000} vs {denseOverall.Precision5:0.000})");
                Console.WriteLine();
                Console.WriteLine($"  • Dense: {accuracyDiff:0.0}% higher precision ({denseOverall.Precision5:0.000} vs {bm25Overall.Precision5:0.000})");
                Console.WriteLine($"           {speedDiff:0.0}% slower ({denseOverall.P95Ms:0.0}ms vs {bm25Overall.P95Ms:0.0}ms)");
            }
            else
            {
                Console.WriteLine($"  • BM25:  P95={bm25Overall.P95Ms:0.0}ms, Precision@5={bm25Overall.Precision5:0.000}");
                Console.WriteLine($"  • Dense: P95={denseOverall.P95Ms:0.0}ms, Precision@5={denseOverall.Precision5:0.000}");
            }

            Console.WriteLine();
        }

        // Performance by query type
        var queryTypes = metrics.Keys
            .Where(k => k.Contains('|') && !k.EndsWith("|Overall"))
            .Select(k => k.Split('|')[1])
            .Distinct()
            .OrderBy(qt => qt)
            .ToList();

        if (queryTypes.Any())
        {
            Console.WriteLine("Performance by Query Type:");
            Console.WriteLine("--------------------------------------------------------------------");

            foreach (var queryType in queryTypes)
            {
                var bm25Key = $"BM25|{queryType}";
                var denseKey = $"Dense|{queryType}";

                if (metrics.TryGetValue(bm25Key, out var bm25Metrics) &&
                    metrics.TryGetValue(denseKey, out var denseMetrics))
                {
                    Console.WriteLine($"\n  {queryType}:");
                    Console.WriteLine($"    BM25:   Precision@5={bm25Metrics.Precision5:0.000}, Recall@5={bm25Metrics.Recall5:0.000}, MRR={bm25Metrics.MRR:0.000}");
                    Console.WriteLine($"    Dense:  Precision@5={denseMetrics.Precision5:0.000}, Recall@5={denseMetrics.Recall5:0.000}, MRR={denseMetrics.MRR:0.000}");

                    // Highlight which strategy performed better for this query type
                    if (denseMetrics.Precision5 > bm25Metrics.Precision5)
                    {
                        var diff = ((denseMetrics.Precision5 - bm25Metrics.Precision5) / bm25Metrics.Precision5) * 100;
                        Console.WriteLine($"    → Dense is {diff:0.0}% more precise for {queryType} queries");
                    }
                    else if (bm25Metrics.Precision5 > denseMetrics.Precision5)
                    {
                        var diff = ((bm25Metrics.Precision5 - denseMetrics.Precision5) / denseMetrics.Precision5) * 100;
                        Console.WriteLine($"    → BM25 is {diff:0.0}% more precise for {queryType} queries");
                    }
                }
            }

            Console.WriteLine();
        }

        // Recommendations
        Console.WriteLine("Recommendations:");
        Console.WriteLine("--------------------------------------------------------------------");

        if (metrics.TryGetValue("BM25|Overall", out var bm25) &&
            metrics.TryGetValue("Dense|Overall", out var dense))
        {
            Console.WriteLine("  Use BM25 when:");
            Console.WriteLine("    • Low latency is critical (faster by ~" + ((dense.P95Ms - bm25.P95Ms) / bm25.P95Ms * 100).ToString("0.0") + "%)");
            Console.WriteLine("    • Queries contain specific keywords or technical terms");
            Console.WriteLine("    • Exact keyword matching is important");
            Console.WriteLine();
            Console.WriteLine("  Use Dense when:");
            Console.WriteLine("    • Semantic understanding is needed");
            Console.WriteLine("    • Handling synonyms and paraphrases");
            Console.WriteLine("    • Higher precision is worth the latency cost");
            Console.WriteLine();
            Console.WriteLine("  Consider Hybrid when:");
            Console.WriteLine("    • You need both keyword matching AND semantic search");
            Console.WriteLine("    • Query types are mixed or unpredictable");
            Console.WriteLine("    • Maximum coverage is required");
        }

        Console.WriteLine();
        Console.WriteLine("====================================================================");
        Console.WriteLine();
    }
}
