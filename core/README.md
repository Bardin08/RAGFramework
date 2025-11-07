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

## Development

Requires:
- .NET 8.0 SDK or later
- Any IDE with C# support (Visual Studio, Rider, VS Code)

## License

Academic research project - KPI, 2025
