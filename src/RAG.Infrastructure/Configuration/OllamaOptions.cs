namespace RAG.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Ollama LLM provider.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// Base URL for Ollama API.
    /// Default: "http://localhost:11434"
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// The Ollama model to use for generation.
    /// Supported: "llama3.1:8b", "llama3.1:70b", "llama2:7b", "mistral:7b", etc.
    /// Default: "llama3.1:8b"
    /// </summary>
    public string Model { get; set; } = "llama3.1:8b";

    /// <summary>
    /// Timeout in seconds for API requests.
    /// Default: 60 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Enable streaming responses.
    /// Default: true
    /// </summary>
    public bool StreamingEnabled { get; set; } = true;

    /// <summary>
    /// Validates that required configuration values are present.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException("Ollama BaseUrl must be specified in configuration.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Ollama Model must be specified in configuration.");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("TimeoutSeconds must be greater than 0.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Ollama BaseUrl '{BaseUrl}' is not a valid URL.");
        }
    }
}
