using System.ComponentModel.DataAnnotations;

namespace RAG.API.Models.Requests;

/// <summary>
/// Query parameters for document list filtering and pagination.
/// </summary>
public class DocumentQueryParameters
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Search term to filter documents by title.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Calculates the number of items to skip for pagination.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;
}
