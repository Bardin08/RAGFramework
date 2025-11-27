#!/bin/bash

# RAG Retrieval Benchmarks Runner (Linux/Mac)
# This script builds and runs the BM25 vs Dense retrieval benchmarks

set -e

echo "=========================================="
echo "  RAG Retrieval Benchmarks Runner"
echo "=========================================="
echo ""

# Check if running from correct directory
if [ ! -f "RAG.Tests.Benchmarks.csproj" ]; then
    echo "Error: Please run this script from the tests/RAG.Tests.Benchmarks directory"
    exit 1
fi

# Check if Elasticsearch is running
echo "Checking prerequisites..."
if ! curl -s http://localhost:9200 > /dev/null; then
    echo "Warning: Elasticsearch may not be running at http://localhost:9200"
    echo "Please start Elasticsearch before running benchmarks"
    exit 1
fi

# Check if Qdrant is running
if ! curl -s http://localhost:6333/health > /dev/null; then
    echo "Warning: Qdrant may not be running at http://localhost:6333"
    echo "Please start Qdrant before running benchmarks"
    exit 1
fi

# Check if embedding service is running
if ! curl -s http://localhost:8001/health > /dev/null; then
    echo "Warning: Embedding service may not be running at http://localhost:8001"
    echo "Please start the Python embedding service before running benchmarks"
    exit 1
fi

echo "All prerequisites met!"
echo ""

# Build the project
echo "Building benchmark project..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

echo ""
echo "Running benchmarks..."
echo "This may take several minutes..."
echo ""

# Run the benchmarks
dotnet run -c Release --no-build

echo ""
echo "Benchmark complete!"
echo "Results have been exported to the Results/ directory"
