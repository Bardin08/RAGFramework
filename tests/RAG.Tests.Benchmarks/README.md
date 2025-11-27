# RAG Retrieval Benchmarks

This project contains automated benchmarks comparing BM25 keyword-based retrieval against Dense semantic retrieval using vector embeddings.

## Purpose

The benchmarks quantitatively evaluate trade-offs between keyword-based (BM25) and semantic (Dense) search for different query types:
- **ExplicitFact**: Direct factual questions (e.g., "What is machine learning?")
- **ImplicitFact**: Questions requiring inference (e.g., "How does NLP work?")
- **InterpretableRationale**: Questions asking "why" (e.g., "Why is chunking important?")
- **HiddenRationale**: Questions requiring deeper reasoning (e.g., "When should you use sparse retrieval?")

## Metrics

The benchmark measures:
- **Precision@5 and @10**: Fraction of retrieved documents that are relevant
- **Recall@5 and @10**: Fraction of relevant documents that were retrieved
- **Mean Reciprocal Rank (MRR)**: Average reciprocal rank of first relevant result
- **Response Time**: p50, p95, p99 latency percentiles

## Prerequisites

Before running benchmarks, ensure the following services are running:

### 1. Elasticsearch
```bash
docker run -d -p 9200:9200 -e "discovery.type=single-node" elasticsearch:8.11.0
```

### 2. Qdrant
```bash
docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant:latest
```

### 3. Python Embedding Service
```bash
cd core/embedding-service
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8001
```

## Configuration

Edit `appsettings.benchmark.json` to configure service endpoints:

```json
{
  "Elasticsearch": {
    "Uri": "http://localhost:9200",
    "IndexName": "rag_benchmark_test"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334
  },
  "Embedding": {
    "ServiceUrl": "http://localhost:8001",
    "Model": "sentence-transformers/all-MiniLM-L6-v2"
  }
}
```

## Running Benchmarks

### Option 1: Using dotnet run
```bash
cd tests/RAG.Tests.Benchmarks
dotnet run -c Release
```

### Option 2: Using scripts

#### Linux/Mac:
```bash
cd tests/RAG.Tests.Benchmarks
chmod +x RunBenchmarks.sh
./RunBenchmarks.sh
```

#### Windows:
```powershell
cd tests\RAG.Tests.Benchmarks
.\RunBenchmarks.ps1
```

## Understanding Results

### Console Output

The benchmark generates a detailed console report:

```
====================================================================
                Benchmark Results: BM25 vs Dense Retrieval
====================================================================

Overall Performance:
--------------------------------------------------------------------
  Strategy   │ Precision@5 │ Recall@5 │   MRR   │  P95 Latency
─────────────┼─────────────┼──────────┼─────────┼──────────────
  BM25       │    0.720    │  0.650   │ 0.780   │   85.0 ms
  Dense      │    0.810    │  0.730   │ 0.840   │   175.0 ms

Trade-offs:
--------------------------------------------------------------------
  • BM25:  106.0% faster (85ms vs 175ms)
           Lower precision (0.720 vs 0.810)

  • Dense: 12.5% higher precision (0.810 vs 0.720)
           106.0% slower (175ms vs 85ms)
```

### Exported Files

Results are exported to the `Results/` directory with timestamps:

1. **CSV Format** (`benchmark-results_YYYY-MM-DD_HH-mm-ss.csv`)
   ```csv
   Strategy,QueryType,QueryCount,Precision@5,Precision@10,Recall@5,Recall@10,MRR,P50_ms,P95_ms,P99_ms
   BM25,Overall,55,0.7200,0.7200,0.6500,0.6500,0.7800,75.50,85.00,92.00
   Dense,Overall,55,0.8100,0.8100,0.7300,0.7300,0.8400,160.00,175.00,190.00
   ```

2. **JSON Format** (`benchmark-results_YYYY-MM-DD_HH-mm-ss.json`)
   ```json
   {
     "Timestamp": "2025-11-26T10:30:00Z",
     "Results": [
       {
         "Strategy": "BM25",
         "QueryType": "Overall",
         "QueryCount": 55,
         "Precision": { "At5": 0.72, "At10": 0.72 },
         "Recall": { "At5": 0.65, "At10": 0.65 },
         "MRR": 0.78,
         "Latency": { "P50_ms": 75.5, "P95_ms": 85.0, "P99_ms": 92.0 }
       }
     ]
   }
   ```

## Dataset

The benchmark uses `Data/benchmark-dataset.json` containing:
- **50 documents**: Covering topics like ML, NLP, RAG, vector search, embeddings
- **55 queries**: With ground truth relevance judgments across 4 query types

### Dataset Format

```json
{
  "documents": [
    {
      "id": "doc-001",
      "text": "Machine learning is a subset of AI...",
      "source": "ml-intro.pdf"
    }
  ],
  "queries": [
    {
      "id": "q-001",
      "text": "What is machine learning?",
      "relevantDocIds": ["doc-001", "doc-005"],
      "queryType": "ExplicitFact"
    }
  ]
}
```

## Benchmark Execution Flow

1. **GlobalSetup**:
   - Load benchmark dataset from JSON
   - Initialize Elasticsearch and Qdrant clients
   - Seed Elasticsearch with documents (BM25 index)
   - Seed Qdrant with documents + embeddings (vector index)

2. **Benchmark Execution**:
   - Run all 55 queries through both BM25 and Dense retrievers
   - Measure retrieval latency for each query
   - Calculate Precision@5, Recall@5, and MRR for each result

3. **GlobalCleanup**:
   - Aggregate metrics by strategy and query type
   - Calculate percentile latencies (p50, p95, p99)
   - Export results to CSV and JSON
   - Generate console report
   - Clean up test data from Elasticsearch and Qdrant

## Reproducibility

The benchmark is designed for reproducibility:
- Fixed random seed for tenant ID generation
- Deterministic dataset loading
- Consistent seeding of data stores
- Multiple iterations (warmup=3, iterations=10) for stable measurements

### Variance

Expect some variance in latency measurements due to:
- Network conditions
- System resource availability
- Cache effects

To minimize variance:
- Run benchmarks on a dedicated machine
- Close other applications
- Run multiple benchmark sessions and compare results

## Customizing the Dataset

To create a custom dataset:

1. Edit `Data/benchmark-dataset.json`
2. Add documents with unique IDs
3. Add queries with:
   - Relevant document IDs (ground truth)
   - Query type classification
4. Ensure all referenced document IDs exist

## Integration with CI/CD

The benchmark can be integrated into CI/CD pipelines for tracking performance trends:

```yaml
# .github/workflows/benchmarks.yml
name: Weekly Benchmarks
on:
  schedule:
    - cron: '0 0 * * 0'  # Every Sunday at midnight

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Start Elasticsearch
        run: docker-compose up -d elasticsearch
      - name: Start Qdrant
        run: docker-compose up -d qdrant
      - name: Start Embedding Service
        run: docker-compose up -d embedding-service
      - name: Run Benchmarks
        run: cd tests/RAG.Tests.Benchmarks && dotnet run -c Release
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results
          path: tests/RAG.Tests.Benchmarks/Results/*.csv
```

## Interpreting Results

### When to use BM25:
- Low latency is critical (~2x faster than Dense)
- Queries contain specific keywords or technical terms
- Exact keyword matching is important
- Working with structured data or codes

### When to use Dense:
- Semantic understanding is needed
- Handling synonyms and paraphrases
- Higher precision is worth the latency cost
- Queries are conversational or vague

### When to consider Hybrid:
- You need both keyword matching AND semantic search
- Query types are mixed or unpredictable
- Maximum coverage is required
- You can tolerate slightly higher latency

## Troubleshooting

### "Failed to connect to Elasticsearch"
- Ensure Elasticsearch is running: `curl http://localhost:9200`
- Check `appsettings.benchmark.json` for correct URI

### "Failed to connect to Qdrant"
- Ensure Qdrant is running: `curl http://localhost:6333/health`
- Check port configuration (6334 for gRPC)

### "Embedding service timeout"
- Ensure Python service is running: `curl http://localhost:8001/health`
- Increase `TimeoutSeconds` in configuration
- Check embedding service logs

### "No relevant documents found"
- Verify dataset was seeded correctly
- Check Elasticsearch index: `curl http://localhost:9200/rag_benchmark_test/_count`
- Check Qdrant collection: Use Qdrant web UI at `http://localhost:6333/dashboard`

## Performance Targets

Based on Epic 3 requirements:
- **BM25**: p95 latency < 100ms
- **Dense**: p95 latency < 200ms
- **Precision@5**: BM25 ≥ 0.70, Dense ≥ 0.75 (estimated)

## License

This benchmark is part of the RAGCore project.
