using System.ComponentModel.DataAnnotations;

namespace RAG.API.DTOs;

/// <summary>
/// Request DTO for Dense semantic retrieval using vector embeddings.
/// </summary>
/// <param name="Query">The search query text. Required, non-empty.</param>
/// <param name="TopK">Maximum number of results to return. Optional, defaults to 10. Must be between 1 and 100.</param>
/// <param name="Threshold">Similarity threshold for filtering results. Optional, defaults to 0.7. Must be between 0.0 and 1.0.</param>
public record DenseRetrievalRequest(
    [Required(ErrorMessage = "Query is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 1000 characters")]
    string Query,

    [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
    int? TopK = null,

    [Range(0.0, 1.0, ErrorMessage = "Threshold must be between 0.0 and 1.0")]
    double? Threshold = null
);
