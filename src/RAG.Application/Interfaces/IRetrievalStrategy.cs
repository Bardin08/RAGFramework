using RAG.Core.Enums;

namespace RAG.Application.Interfaces;

/// <summary>
/// Extended interface for retrieval strategies that adds strategy identification and metadata.
/// Extends IRetriever to maintain backward compatibility while adding strategy-specific functionality.
/// </summary>
public interface IRetrievalStrategy : IRetriever
{
    /// <summary>
    /// Gets the human-readable name of the retrieval strategy.
    /// Used for logging, metrics, and API responses.
    /// </summary>
    /// <returns>Strategy name (e.g., "BM25", "Dense", "Hybrid").</returns>
    string GetStrategyName();

    /// <summary>
    /// Gets the type of retrieval strategy as an enum value.
    /// Used for factory pattern and strategy selection logic.
    /// </summary>
    RetrievalStrategyType StrategyType { get; }
}
