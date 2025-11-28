using BenchmarkDotNet.Running;
using RAG.Tests.Benchmarks.Retrievers;

namespace RAG.Tests.Benchmarks;

/// <summary>
/// Entry point for running RAG retrieval benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   RAG Retrieval Benchmarks: BM25 vs Dense");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("This benchmark compares BM25 keyword-based retrieval");
        Console.WriteLine("against Dense semantic retrieval using vector embeddings.");
        Console.WriteLine();
        Console.WriteLine("Prerequisites:");
        Console.WriteLine("  - Elasticsearch running (default: http://localhost:9200)");
        Console.WriteLine("  - Qdrant running (default: localhost:6334)");
        Console.WriteLine("  - Python embedding service (default: http://localhost:8001)");
        Console.WriteLine();
        Console.WriteLine("Starting benchmark...");
        Console.WriteLine();

        BenchmarkRunner.Run<RetrievalBenchmarks>(args: args);
    }
}
