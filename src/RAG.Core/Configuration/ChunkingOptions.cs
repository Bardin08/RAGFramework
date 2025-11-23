namespace RAG.Core.Configuration;

/// <summary>
/// Configuration options for document chunking strategy.
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// The size of each chunk in tokens. Default is 512.
    /// </summary>
    public int ChunkSize { get; set; } = 512;

    /// <summary>
    /// The number of overlapping tokens between consecutive chunks. Default is 128.
    /// </summary>
    public int OverlapSize { get; set; } = 128;

    /// <summary>
    /// Validates the chunking configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (ChunkSize <= 0)
            throw new InvalidOperationException("ChunkSize must be greater than 0");
        if (OverlapSize < 0)
            throw new InvalidOperationException("OverlapSize must be >= 0");
        if (OverlapSize >= ChunkSize)
            throw new InvalidOperationException("OverlapSize must be less than ChunkSize");
    }
}
