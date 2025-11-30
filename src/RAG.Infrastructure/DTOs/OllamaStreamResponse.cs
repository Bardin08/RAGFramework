using System.Text.Json.Serialization;

namespace RAG.Infrastructure.DTOs;

/// <summary>
/// Streaming response from Ollama /api/generate endpoint.
/// Each line is a separate JSON object.
/// </summary>
public class OllamaStreamResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}
