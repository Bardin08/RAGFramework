using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Document filter that adds descriptions to Swagger tags for better organization.
/// </summary>
public class SwaggerTagDescriptionsDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Applies tag descriptions to the OpenAPI document.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
    /// <param name="context">The document filter context.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new OpenApiTag
            {
                Name = "Query",
                Description = "RAG query operations for question answering. Supports both streaming and non-streaming responses with configurable retrieval strategies and LLM providers."
            },
            new OpenApiTag
            {
                Name = "Documents",
                Description = "Document management operations including upload, listing, retrieval, and deletion. Supports PDF, DOCX, and TXT file formats with automatic text extraction and indexing."
            },
            new OpenApiTag
            {
                Name = "Retrieval",
                Description = "Document retrieval operations using BM25 (keyword-based), Dense (semantic), and Hybrid strategies. Returns relevant document chunks with relevance scores."
            },
            new OpenApiTag
            {
                Name = "Health",
                Description = "Health check endpoints for monitoring and Kubernetes orchestration. Includes liveness, readiness, and detailed health status endpoints."
            }
        };
    }
}
