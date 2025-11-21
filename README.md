# RAG Framework

![CI](https://github.com/vladyslavbardin/thesis-core/workflows/CI/badge.svg)

A Retrieval-Augmented Generation (RAG) framework built with .NET 8 and Clean Architecture principles.

## Project Structure

```
RAGFramework/
├── src/
│   ├── RAG.Core/           # Domain entities, interfaces, DTOs
│   ├── RAG.Application/    # Business logic, use cases, application services
│   ├── RAG.Infrastructure/ # Data access, external API clients, implementations
│   └── RAG.API/            # HTTP endpoints, controllers/minimal APIs
└── tests/
    ├── RAG.Tests.Unit/        # Unit tests
    └── RAG.Tests.Integration/ # Integration tests
```

## Technology Stack

- **.NET 8.0** - LTS runtime
- **C# 12** - Modern language features
- **xUnit** - Testing framework
- **Serilog** - Structured logging
- **FluentValidation** - Input validation
- **Clean Architecture** - Dependency inversion and separation of concerns

## Build & Test

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build --configuration Release

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## CI/CD

This project uses GitHub Actions for continuous integration. Every push and pull request triggers:
- Build verification
- Unit and integration tests
- Code coverage collection

## Health Check Endpoints

The RAG API provides health check endpoints for monitoring and orchestration:

### Kubernetes Probes

**Liveness Probe** (`/healthz` or `/healthz/live`):
```bash
curl http://localhost:5000/healthz
# Returns: 200 OK with "OK" text
```

**Readiness Probe** (`/healthz/ready`):
```bash
curl http://localhost:5000/healthz/ready
# Returns: 200 OK if all services healthy, 503 Service Unavailable otherwise
```

### Detailed Health Status

**Admin Health** (`/api/admin/health`):
```bash
curl http://localhost:5000/api/admin/health
```

Returns detailed JSON with status of all services:
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-08T20:00:00Z",
  "version": "1.0.0",
  "services": {
    "elasticsearch": {
      "status": "Healthy",
      "responseTime": "8ms"
    },
    "qdrant": {
      "status": "Healthy",
      "responseTime": "6ms"
    },
    "keycloak": {
      "status": "Healthy",
      "responseTime": "12ms"
    },
    "embeddingService": {
      "status": "Healthy",
      "responseTime": "15ms"
    }
  }
}
```

### Features

- **5-second timeout** per service health check
- **10-second caching** to reduce load on dependencies
- **Concurrent checks** for faster overall health status
- **Detailed logging** with Serilog integration

## Development

Requires:
- .NET 8.0 SDK or later
- Any IDE with C# support (Visual Studio, Rider, VS Code)
- Docker Compose (for infrastructure services)

## License

Academic research project - KPI, 2025
