namespace RAG.Core.Domain;

/// <summary>
/// Represents a source citation from the retrieval context used in generating a response.
/// Enables traceability and verification of generated answers.
/// </summary>
/// <param name="SourceId">Unique identifier of the source document.</param>
/// <param name="Title">Title or name of the source document.</param>
/// <param name="Excerpt">Relevant excerpt from the source used in generation.</param>
/// <param name="Score">Relevance score of the source (0.0-1.0).</param>
public record SourceReference(
    Guid SourceId,
    string Title,
    string Excerpt,
    double Score);
