using System.ComponentModel.DataAnnotations;

namespace RAG.API.DTOs;

/// <summary>
/// Request model for non-streaming RAG queries.
/// </summary>
public class QueryRequest
{
    /// <summary>
    /// The user's query text.
    /// </summary>
    [Required(ErrorMessage = "Query is required")]
    [MinLength(1, ErrorMessage = "Query cannot be empty")]
    [MaxLength(2000, ErrorMessage = "Query cannot exceed 2000 characters")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Number of top results to retrieve (default: 10).
    /// </summary>
    [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
    public int? TopK { get; set; }

    /// <summary>
    /// Optional retrieval strategy override (BM25, Dense, Hybrid, Adaptive).
    /// If not specified, uses the default strategy from configuration.
    /// </summary>
    [MaxLength(50, ErrorMessage = "Strategy cannot exceed 50 characters")]
    public string? Strategy { get; set; }

    /// <summary>
    /// Optional LLM provider override (OpenAI, Ollama).
    /// If not specified, uses the default provider from configuration.
    /// </summary>
    [MaxLength(50, ErrorMessage = "Provider cannot exceed 50 characters")]
    public string? Provider { get; set; }

    /// <summary>
    /// Optional temperature override for LLM generation (0.0 to 1.0).
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Temperature must be between 0.0 and 1.0")]
    public double? Temperature { get; set; }

    /// <summary>
    /// Optional prompt template name override.
    /// If not specified, uses the default template from configuration.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Template cannot exceed 100 characters")]
    public string? Template { get; set; }

    /// <summary>
    /// Optional tenant identifier for multi-tenancy.
    /// </summary>
    [MaxLength(100, ErrorMessage = "TenantId cannot exceed 100 characters")]
    public string? TenantId { get; set; }

    /// <summary>
    /// Optional max tokens override for LLM generation.
    /// </summary>
    [Range(1, 4000, ErrorMessage = "MaxTokens must be between 1 and 4000")]
    public int? MaxTokens { get; set; }
}
