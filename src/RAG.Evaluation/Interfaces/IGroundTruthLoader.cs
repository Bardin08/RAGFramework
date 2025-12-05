using RAG.Evaluation.Models;

namespace RAG.Evaluation.Interfaces;

/// <summary>
/// Interface for loading ground truth data from various formats.
/// </summary>
public interface IGroundTruthLoader
{
    /// <summary>
    /// Gets the supported file extensions for this loader (e.g., ".json", ".csv").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Determines if this loader can handle the specified file path.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if this loader can handle the file format.</returns>
    bool CanHandle(string path);

    /// <summary>
    /// Loads ground truth data from the specified file.
    /// </summary>
    /// <param name="path">The file path to load from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded ground truth dataset.</returns>
    Task<GroundTruthDataset> LoadAsync(string path, CancellationToken cancellationToken = default);
}
