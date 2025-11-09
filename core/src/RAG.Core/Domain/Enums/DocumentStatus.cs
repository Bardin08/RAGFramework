namespace RAG.Core.Domain.Enums;

/// <summary>
/// Represents the status of a document in the RAG system.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Document has been uploaded but not yet processed.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Document is being processed (chunked and embedded).
    /// </summary>
    Processing,

    /// <summary>
    /// Document processing completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Document processing failed.
    /// </summary>
    Failed
}
