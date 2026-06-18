namespace NeuroMentor.Api.Services;

/// <summary>
/// Abstraction over LLM providers (Anthropic, OpenAI, etc.).
/// </summary>
public interface IAiService
{
  /// <summary>
  /// Single-turn completion: sends a system prompt + user message and returns the full response text.
  /// </summary>
  Task<string> CompleteAsync(string system, string userPrompt, int maxTokens = 2000);

  /// <summary>
  /// Streaming multi-turn completion: yields text chunks as they arrive from the provider.
  /// </summary>
  IAsyncEnumerable<string> StreamAsync(
      string system,
      List<object> messages,
      int maxTokens = 1500,
      CancellationToken ct = default);
}
