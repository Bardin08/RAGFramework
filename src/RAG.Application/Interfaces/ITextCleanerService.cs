namespace RAG.Application.Interfaces;

/// <summary>
/// Service for cleaning and normalizing extracted text.
/// </summary>
public interface ITextCleanerService
{
    /// <summary>
    /// Cleans and normalizes extracted text.
    /// </summary>
    /// <param name="text">The raw extracted text.</param>
    /// <returns>Cleaned and normalized text.</returns>
    string CleanText(string text);
}
