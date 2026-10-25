using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatDown.Core.Llm;

public sealed record ChatMessage(string Role, string Content);

public sealed class LlmException(string message) : Exception(message);

public interface ILlmClient
{
    Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}

// Any service speaking the OpenAI chat completions API: OpenAI, DeepSeek, Qwen, Ollama, and others.
public sealed class OpenAiCompatibleClient(HttpClient http, string baseUrl, string model, string? apiKey) : ILlmClient
{
    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = 0.2,
            ["messages"] = new JsonArray(messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content }).ToArray()),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new LlmException(e.Message);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new LlmException($"HTTP {(int)response.StatusCode}: {Shorten(text)}");
            try
            {
                var choices = JsonNode.Parse(text)?["choices"] as JsonArray;
                var content = choices is { Count: > 0 } ? choices[0]?["message"]?["content"]?.GetValue<string>() : null;
                if (string.IsNullOrWhiteSpace(content))
                    throw new LlmException("The model returned an empty reply.");
                return content.Trim();
            }
            catch (JsonException)
            {
                throw new LlmException($"Unexpected reply: {Shorten(text)}");
            }
        }
    }

    static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
