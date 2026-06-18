using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/review")]
[Authorize]
public class ReviewController(AppDbContext db, IAiService claude) : ControllerBase
{
  private bool HasAiAccess => User.FindFirstValue("isAiEnabled") == "True";

  /// <summary>Gera um guia de revisão personalizado com base nos erros do aluno.</summary>
  [HttpPost("generate")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(500)]
  public async Task<IActionResult> Generate(GenerateReviewRequest req)
  {
    if (!HasAiAccess) return Forbid();
    var lesson = req.LessonId.HasValue ? await db.Lessons.FindAsync(req.LessonId.Value) : null;
    var material = req.Context ?? lesson?.RawText ?? "";

    var wrongList = string.Join("\n", req.WrongAnswers.Select((q, i) => $"{i + 1}. {q}"));

    var matRef = material.Length > 0 ? $"\nMaterial de referência:\n{material[..Math.Min(10000, material.Length)]}" : "";
    var prompt = $$"""
            O aluno errou as seguintes questões:
            {{wrongList}}
            {{matRef}}

            Crie um guia de revisão focado nos pontos fracos do aluno. Retorne APENAS o JSON:
            {
              "topics": [
                {
                  "title": "Tópico para revisar",
                  "explanation": "Explicação clara e didática",
                  "tips": ["dica 1", "dica 2"]
                }
              ],
              "summary": "Resumo geral do que precisa ser reforçado"
            }
            """;

    var raw = await claude.CompleteAsync(NeuroPersona.ReviewPlanner, prompt, 1500);
    var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
    if (start == -1 || end == -1) return StatusCode(500, new { error = "Resposta inválida da IA." });

    return Ok(System.Text.Json.JsonDocument.Parse(raw[start..(end + 1)]).RootElement);
  }
}
