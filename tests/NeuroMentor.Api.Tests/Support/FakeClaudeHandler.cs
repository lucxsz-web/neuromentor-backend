using System.Net;
using System.Text;
using System.Text.Json;

namespace NeuroMentor.Api.Tests.Support;

/// <summary>
/// Handler HTTP falso que substitui a chamada real à API da Anthropic.
/// - Retorna um texto canônico configurável (<see cref="ResponseText"/>) no formato
///   esperado por <c>ClaudeService.CompleteAsync</c>: { "content": [ { "text": ... } ] }.
/// - Captura o último payload e o system prompt enviados, para permitir asserções de
///   "a IA recebeu o material correto" (cobre o risco do PDF de a IA responder fora do material).
/// </summary>
public class FakeClaudeHandler : HttpMessageHandler
{
    /// <summary>Texto que a "IA" devolverá dentro de content[0].text.</summary>
    public string ResponseText { get; set; } = "{}";

    /// <summary>Corpo bruto da última requisição enviada à IA.</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>System prompt extraído da última requisição.</summary>
    public string? LastSystemPrompt { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(LastRequestBody);
            if (doc.RootElement.TryGetProperty("system", out var sys))
                LastSystemPrompt = sys.GetString();
        }

        var anthropicResponse = new
        {
            content = new[] { new { type = "text", text = ResponseText } }
        };
        var json = JsonSerializer.Serialize(anthropicResponse);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
