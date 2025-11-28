using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Enums;
using RAG.Infrastructure.Retrievers;

namespace RAG.Infrastructure.Factories;

/// <summary>
/// Factory for creating retrieval strategy instances based on strategy type.
/// Uses dependency injection to resolve concrete retriever implementations.
/// </summary>
public class RetrievalStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetrievalStrategyFactory> _logger;

    public RetrievalStrategyFactory(
        IServiceProvider serviceProvider,
        ILogger<RetrievalStrategyFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a retrieval strategy instance of the specified type.
    /// </summary>
    /// <param name="type">The type of retrieval strategy to create.</param>
    /// <returns>An instance of the requested retrieval strategy.</returns>
    /// <exception cref="ArgumentException">Thrown when an unknown strategy type is provided.</exception>
    public IRetrievalStrategy CreateStrategy(RetrievalStrategyType type)
    {
        _logger.LogDebug("Creating retrieval strategy: {StrategyType}", type);

        IRetrievalStrategy strategy = type switch
        {
            RetrievalStrategyType.BM25 => _serviceProvider.GetRequiredService<BM25Retriever>(),
            RetrievalStrategyType.Dense => _serviceProvider.GetRequiredService<DenseRetriever>(),
            RetrievalStrategyType.Hybrid => _serviceProvider.GetRequiredService<HybridRetriever>(),
            _ => throw new ArgumentException($"Unknown retrieval strategy type: {type}", nameof(type))
        };

        _logger.LogInformation(
            "Created retrieval strategy: {StrategyName} (Type: {StrategyType})",
            strategy.GetStrategyName(), type);

        return strategy;
    }
}
