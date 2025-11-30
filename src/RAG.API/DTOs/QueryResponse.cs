namespace RAG.API.DTOs;

/// <summary>
/// Response model for non-streaming RAG queries.
/// </summary>
public class QueryResponse
{
    /// <summary>
    /// The generated answer from the LLM.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// List of source documents used to generate the answer.
    /// </summary>
    public List<SourceDto> Sources { get; set; } = new();

    /// <summary>
    /// Metadata about the query execution.
    /// </summary>
    public QueryMetadataDto Metadata { get; set; } = new();
}

/// <summary>
/// Source document information.
/// </summary>
public class SourceDto
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document title or filename.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Relevant text excerpt from the document.
    /// </summary>
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score (0.0 to 1.0).
    /// </summary>
    public double Score { get; set; }
}

/// <summary>
/// Metadata about query execution.
/// </summary>
public class QueryMetadataDto
{
    /// <summary>
    /// Time spent on retrieval (e.g., "120ms").
    /// </summary>
    public string RetrievalTime { get; set; } = string.Empty;

    /// <summary>
    /// Time spent on LLM generation (e.g., "1500ms").
    /// </summary>
    public string GenerationTime { get; set; } = string.Empty;

    /// <summary>
    /// Total query processing time (e.g., "1620ms").
    /// </summary>
    public string TotalTime { get; set; } = string.Empty;

    /// <summary>
    /// Number of tokens used by the LLM.
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// LLM model used for generation.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Retrieval strategy used (BM25, Dense, Hybrid, Adaptive).
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for the answer (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Whether the response was served from cache.
    /// </summary>
    public bool FromCache { get; set; }
}
