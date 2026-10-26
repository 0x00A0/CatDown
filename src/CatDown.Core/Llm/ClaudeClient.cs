using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace CatDown.Core.Llm;

// Claude through Anthropic's official SDK and the Messages API.
public sealed class ClaudeClient(IAnthropicClient client, string model) : ILlmClient
{
    public const string FallbackBeta = "server-side-fallback-2026-07-01";

    // Models that accept server-side refusal fallbacks with the "default" routing.
    static readonly HashSet<string> FallbackModels = ["claude-fable-5-1", "claude-opus-5-5", "claude-opus-5", "claude-sonnet-5-5"];

    public static ClaudeClient Create(string apiKey, string model, HttpClient? http = null)
    {
        var client = http is null ? new AnthropicClient { ApiKey = apiKey } : new AnthropicClient { ApiKey = apiKey, HttpClient = http };
        return new ClaudeClient(client, model);
    }

    public static bool UsesFallbacks(string model) => FallbackModels.Contains(model);

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var system = string.Join("\n\n", messages.Where(m => m.Role == "system").Select(m => m.Content));
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            // Medium is Claude Opus 5.5's own default; set it so other models behave the same.
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            Messages = messages
                .Where(m => m.Role != "system")
                .Select(m => m.Role == "assistant"
                    ? (BetaMessageParam)new BetaAssistantMessageParam { Content = m.Content }
                    : new BetaUserMessageParam { Content = m.Content })
                .ToList(),
        };
        if (system.Length > 0)
            parameters = parameters with { System = system };
        if (UsesFallbacks(model))
            parameters = parameters with { Betas = [FallbackBeta], Fallbacks = new Default() };

        BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(parameters, cancellationToken);
        }
        catch (AnthropicApiException e)
        {
            throw new LlmException(e.Message);
        }
        catch (HttpRequestException e)
        {
            throw new LlmException(e.Message);
        }

        if (response.StopReason == "refusal")
            throw new LlmException($"Claude declined the request: {response.StopDetails?.Explanation ?? response.StopDetails?.Category ?? "no details"}");

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)).Trim();
        if (text.Length == 0)
            throw new LlmException("The model returned an empty reply.");
        return text;
    }
}
