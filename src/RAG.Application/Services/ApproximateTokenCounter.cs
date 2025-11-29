namespace RAG.Application.Services;

/// <summary>
/// Simple token counter using character count approximation.
/// Approximation: 1 token ≈ 4 characters (conservative estimate for English text).
/// </summary>
public class ApproximateTokenCounter : ITokenCounter
{
    /// <summary>
    /// Counts tokens using character count / 4 approximation.
    /// </summary>
    /// <param name="text">Text to count tokens in.</param>
    /// <returns>Approximate token count.</returns>
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Conservative approximation: 1 token ≈ 4 characters
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
