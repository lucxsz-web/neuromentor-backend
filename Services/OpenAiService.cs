using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace NeuroMentor.Api.Services;

public class OpenAiService(HttpClient http, IConfiguration config) : IAiService
{
  private string ApiKey => config["OpenAI:ApiKey"]!;
  private string Model => config["OpenAI:Model"] ?? "gpt-4o";

  public async Task<string> CompleteAsync(string system, string userPrompt, int maxTokens = 2000)
  {
    var payload = new
    {
      model = Model,
      max_tokens = maxTokens,
      messages = new object[]
        {
                new { role = "system", content = system },
                new { role = "user", content = userPrompt },
        }
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
    req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    var res = await http.SendAsync(req);
    res.EnsureSuccessStatusCode();

    var json = await res.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(json);
    return doc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString() ?? "";
  }

  public async IAsyncEnumerable<string> StreamAsync(
      string system,
      List<object> messages,
      int maxTokens = 1500,
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    // Build OpenAI messages array: system + conversation messages
    var openAiMessages = new List<object> { new { role = "system", content = system } };

    foreach (var msg in messages)
    {
      // Messages come as anonymous objects with role/content from the controllers
      openAiMessages.Add(msg);
    }

    var payload = new
    {
      model = Model,
      max_tokens = maxTokens,
      stream = true,
      messages = openAiMessages,
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
    req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    res.EnsureSuccessStatusCode();

    await using var stream = await res.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(stream);

    while (!ct.IsCancellationRequested)
    {
      var line = await reader.ReadLineAsync(ct);
      if (line is null) break;
      if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
      var data = line[6..];
      if (data == "[DONE]") break;

      using var doc = JsonDocument.Parse(data);
      var root = doc.RootElement;
      if (root.TryGetProperty("choices", out var choices))
      {
        var delta = choices[0].GetProperty("delta");
        if (delta.TryGetProperty("content", out var content))
        {
          var text = content.GetString();
          if (!string.IsNullOrEmpty(text))
            yield return text;
        }
      }
    }
  }
}
