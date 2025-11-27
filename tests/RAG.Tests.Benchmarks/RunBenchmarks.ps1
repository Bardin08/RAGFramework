# RAG Retrieval Benchmarks Runner (Windows PowerShell)
# This script builds and runs the BM25 vs Dense retrieval benchmarks

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  RAG Retrieval Benchmarks Runner" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running from correct directory
if (-not (Test-Path "RAG.Tests.Benchmarks.csproj")) {
    Write-Host "Error: Please run this script from the tests\RAG.Tests.Benchmarks directory" -ForegroundColor Red
    exit 1
}

# Check if Elasticsearch is running
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri "http://localhost:9200" -TimeoutSec 2 -UseBasicParsing
    Write-Host "  ✓ Elasticsearch is running" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Elasticsearch may not be running at http://localhost:9200" -ForegroundColor Red
    Write-Host "    Please start Elasticsearch before running benchmarks" -ForegroundColor Red
    exit 1
}

# Check if Qdrant is running
try {
    $response = Invoke-WebRequest -Uri "http://localhost:6333/health" -TimeoutSec 2 -UseBasicParsing
    Write-Host "  ✓ Qdrant is running" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Qdrant may not be running at http://localhost:6333" -ForegroundColor Red
    Write-Host "    Please start Qdrant before running benchmarks" -ForegroundColor Red
    exit 1
}

# Check if embedding service is running
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8001/health" -TimeoutSec 2 -UseBasicParsing
    Write-Host "  ✓ Embedding service is running" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Embedding service may not be running at http://localhost:8001" -ForegroundColor Red
    Write-Host "    Please start the Python embedding service before running benchmarks" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All prerequisites met!" -ForegroundColor Green
Write-Host ""

# Build the project
Write-Host "Building benchmark project..." -ForegroundColor Yellow
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Running benchmarks..." -ForegroundColor Yellow
Write-Host "This may take several minutes..." -ForegroundColor Yellow
Write-Host ""

# Run the benchmarks
dotnet run -c Release --no-build

Write-Host ""
Write-Host "Benchmark complete!" -ForegroundColor Green
Write-Host "Results have been exported to the Results\ directory" -ForegroundColor Green
