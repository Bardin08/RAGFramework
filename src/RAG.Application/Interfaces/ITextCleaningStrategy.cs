namespace RAG.Application.Interfaces;

/// <summary>
/// Strategy interface for different text cleaning approaches.
/// </summary>
public interface ITextCleaningStrategy
{
    /// <summary>
    /// Gets the name of this cleaning strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies this cleaning strategy to the input text.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <returns>The cleaned text.</returns>
    string Apply(string text);

    /// <summary>
    /// Determines if this strategy should be applied based on the text characteristics.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>True if this strategy is applicable, false otherwise.</returns>
    bool IsApplicable(string text) => true;
}
