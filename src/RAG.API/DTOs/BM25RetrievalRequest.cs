using System.ComponentModel.DataAnnotations;

namespace RAG.API.DTOs;

/// <summary>
/// Request DTO for BM25 keyword-based retrieval.
/// </summary>
/// <param name="Query">The search query text. Required, non-empty.</param>
/// <param name="TopK">Maximum number of results to return. Optional, defaults to 10. Must be between 1 and 100.</param>
public record BM25RetrievalRequest(
    [Required(ErrorMessage = "Query is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 1000 characters")]
    string Query,

    [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
    int? TopK = null
);
