using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroMentor.Api.DTOs.Exercises;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController(IAiService claude, AppDbContext db) : ControllerBase
{
  private static readonly string MentorSystem = NeuroPersona.Mentor;
  private bool HasAiAccess => User.FindFirstValue("isAiEnabled") == "True";

  private async Task<string> BuildContext(ChatRequest req)
  {
    // Prefer module chunk (focused, small) over raw context (large)
    if (req.ModuleId.HasValue)
    {
      var module = await db.LessonModules.FindAsync(req.ModuleId.Value);
      if (module is not null)
      {
        var ctx = $"MÓDULO: {module.Title}\nRESUMO: {module.Summary}\nCONCEITOS: {string.Join(", ", module.Concepts)}";
        if (!string.IsNullOrWhiteSpace(module.TextChunk))
          ctx += $"\n\nTRECHO DO MATERIAL:\n{module.TextChunk}";
        return ctx;
      }
    }

    // Fallback: use raw context truncated
    if (req.Context is not null)
      return req.Context[..Math.Min(8000, req.Context.Length)];

    return "";
  }

  /// <summary>Envia mensagem ao tutor IA e recebe resposta via Server-Sent Events.</summary>
  [HttpPost("stream")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
  {
    if (!HasAiAccess) { Response.StatusCode = 403; return; }
    Response.ContentType = "text/event-stream";
    Response.Headers["Cache-Control"] = "no-cache";
    Response.Headers["X-Accel-Buffering"] = "no";

    var ctx = await BuildContext(req);
    var system = ctx.Length > 0
        ? $"{MentorSystem}\n\n<material>\n{ctx}\n</material>"
        : MentorSystem;

    var messages = req.Messages
        .Select(m => (object)new { role = m.Role, content = m.Content })
        .ToList();

    await foreach (var chunk in claude.StreamAsync(system, messages, ct: ct))
    {
      if (ct.IsCancellationRequested) break;
      var data = $"0:{JsonSerializer.Serialize(chunk)}\n";
      await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(data), ct);
      await Response.Body.FlushAsync(ct);
    }
  }

  /// <summary>Gera exercícios a partir do contexto de chat via IA.</summary>
  [HttpPost("exercises")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(500)]
  public async Task<IActionResult> GenerateExercises([FromBody] ChatRequest req)
  {
    if (!HasAiAccess) return Forbid();
    var ctx = await BuildContext(req);
    var system = ctx.Length > 0
        ? $"{MentorSystem}\n\n<material>\n{ctx}\n</material>"
        : MentorSystem;

    var lastMsg = req.Messages.LastOrDefault();
    var userPrompt = lastMsg?.Content?.ToString() ?? "Gere 3 exercícios variados sobre o material.";

    var prompt = $$"""
            {{userPrompt}}
            Retorne APENAS o JSON:
            {
              "exercises": [
                {
                  "id": "ex-1",
                  "question": "pergunta",
                  "type": "multiple_choice",
                  "options": ["A", "B", "C", "D"]
                }
              ]
            }
            """;

    var raw = await claude.CompleteAsync(system, prompt, 1500);
    var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
    if (start == -1 || end == -1) return StatusCode(500, new { error = "Falha ao gerar exercícios." });

    return Ok(JsonDocument.Parse(raw[start..(end + 1)]).RootElement);
  }
}
