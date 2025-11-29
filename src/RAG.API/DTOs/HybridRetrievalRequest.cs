using System.ComponentModel.DataAnnotations;

namespace RAG.API.DTOs;

/// <summary>
/// Request DTO for hybrid retrieval combining BM25 and Dense strategies.
/// </summary>
/// <param name="Query">The search query text. Required, non-empty.</param>
/// <param name="TopK">Maximum number of results to return. Optional, defaults to 10. Must be between 1 and 100.</param>
/// <param name="Alpha">Weight for BM25 results in weighted scoring. Optional, defaults to 0.5. Must be between 0 and 1. Alpha + Beta must equal 1.0.</param>
/// <param name="Beta">Weight for Dense results in weighted scoring. Optional, defaults to 0.5. Must be between 0 and 1. Alpha + Beta must equal 1.0.</param>
public record HybridRetrievalRequest(
    [Required(ErrorMessage = "Query is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 1000 characters")]
    string Query,

    [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
    int? TopK = null,

    [Range(0.0, 1.0, ErrorMessage = "Alpha must be between 0 and 1")]
    double? Alpha = null,

    [Range(0.0, 1.0, ErrorMessage = "Beta must be between 0 and 1")]
    double? Beta = null
)
{
    /// <summary>
    /// Validates that Alpha + Beta equals 1.0 (if both are provided).
    /// </summary>
    public void ValidateWeights()
    {
        if (Alpha.HasValue && Beta.HasValue)
        {
            var sum = Alpha.Value + Beta.Value;
            if (Math.Abs(sum - 1.0) > 0.001) // Allow small floating point tolerance
            {
                throw new ValidationException($"Alpha + Beta must equal 1.0. Current sum: {sum}");
            }
        }
    }
};
