namespace RAG.Infrastructure.Configuration;

/// <summary>
/// Configuration options for OpenAI LLM provider.
/// </summary>
public class OpenAIOptions
{
    /// <summary>
    /// OpenAI API key for authentication.
    /// Should be stored in User Secrets (development) or environment variables (production).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The OpenAI model to use for generation.
    /// Supported: "gpt-4", "gpt-4-turbo", "gpt-4-turbo-preview", "gpt-3.5-turbo"
    /// Default: "gpt-4-turbo"
    /// </summary>
    public string Model { get; set; } = "gpt-4-turbo";

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout in seconds for API requests.
    /// Default: 60 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Validates that required configuration values are present.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. " +
                "Set 'LLMProviders:OpenAI:ApiKey' in User Secrets (development) or environment variables (production).");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("OpenAI Model must be specified in configuration.");
        }

        if (MaxRetries < 0)
        {
            throw new InvalidOperationException("MaxRetries must be non-negative.");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("TimeoutSeconds must be greater than 0.");
        }
    }
}
