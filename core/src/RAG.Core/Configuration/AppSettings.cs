using System.ComponentModel.DataAnnotations;

namespace RAG.Core.Configuration;

/// <summary>
/// Root configuration settings for the RAG application.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the Elasticsearch settings.
    /// </summary>
    public ElasticsearchSettings Elasticsearch { get; set; } = new();

    /// <summary>
    /// Gets or sets the Qdrant settings.
    /// </summary>
    public QdrantSettings Qdrant { get; set; } = new();

    /// <summary>
    /// Gets or sets the Embedding Service settings.
    /// </summary>
    public EmbeddingServiceSettings EmbeddingService { get; set; } = new();

    /// <summary>
    /// Gets or sets the OpenAI settings.
    /// </summary>
    public OpenAISettings OpenAI { get; set; } = new();

    /// <summary>
    /// Gets or sets the Ollama settings.
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Gets or sets the Keycloak settings.
    /// </summary>
    public KeycloakSettings Keycloak { get; set; } = new();
}

/// <summary>
/// Configuration settings for Elasticsearch.
/// </summary>
public class ElasticsearchSettings
{
    /// <summary>
    /// Gets or sets the Elasticsearch URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for Qdrant vector database.
/// </summary>
public class QdrantSettings
{
    /// <summary>
    /// Gets or sets the Qdrant URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for the Embedding Service.
/// </summary>
public class EmbeddingServiceSettings
{
    /// <summary>
    /// Gets or sets the Embedding Service URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for OpenAI API.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Gets or sets the OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenAI model to use.
    /// </summary>
    [Required]
    public string Model { get; set; } = "gpt-4-turbo";

    /// <summary>
    /// Gets or sets the maximum number of tokens for generation.
    /// </summary>
    [Range(1, 4096)]
    public int MaxTokens { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the temperature for generation (0.0 - 2.0).
    /// </summary>
    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.7f;
}

/// <summary>
/// Configuration settings for Ollama local LLM.
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Gets or sets the Ollama URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Ollama model to use.
    /// </summary>
    [Required]
    public string Model { get; set; } = "llama3.1:8b";
}

/// <summary>
/// Configuration settings for Keycloak authentication.
/// </summary>
public class KeycloakSettings
{
    /// <summary>
    /// Gets or sets the Keycloak authority URL.
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API audience identifier.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;
}
