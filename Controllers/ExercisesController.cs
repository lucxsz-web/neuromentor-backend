using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/exercises")]
[Authorize]
public class ExercisesController(AppDbContext db, IAiService claude) : ControllerBase
{
  private bool HasAiAccess => User.FindFirstValue("isAiEnabled") == "True";

  /// <summary>Gera exercícios via IA com base no material da aula.</summary>
  [HttpPost("generate")]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(500)]
  public async Task<IActionResult> Generate(GenerateExercisesRequest req)
  {
    if (!HasAiAccess) return Forbid();
    var lesson = req.LessonId.HasValue ? await db.Lessons.FindAsync(req.LessonId.Value) : null;
    var material = req.Context ?? lesson?.RawText ?? "";

    if (string.IsNullOrWhiteSpace(material))
      return BadRequest(new { error = "Contexto não encontrado." });

    var mat = material[..Math.Min(12000, material.Length)];
    var prompt = $$"""
            Crie {{req.Count}} exercícios sobre o módulo "{{req.ModuleTitle}}" baseado na Taxonomia de Bloom.
            Material:
            {{mat}}

            Retorne APENAS o JSON:
            {
              "exercises": [
                {
                  "id": "ex-1",
                  "question": "pergunta",
                  "type": "multiple_choice",
                  "options": ["A", "B", "C", "D"]
                },
                {
                  "id": "ex-2",
                  "question": "pergunta discursiva",
                  "type": "open"
                }
              ]
            }
            Varie entre questões de múltipla escolha (type: multiple_choice) e dissertativas (type: open).
            """;

    var raw = await claude.CompleteAsync(NeuroPersona.ExerciseGenerator, prompt, 1500);
    var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
    if (start == -1 || end == -1) return StatusCode(500, new { error = "Resposta inválida da IA." });

    return Ok(JsonDocument.Parse(raw[start..(end + 1)]).RootElement);
  }

  /// <summary>Corrige a resposta de um exercício usando IA.</summary>
  [HttpPost("correct")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(500)]
  public async Task<IActionResult> Correct(CorrectExerciseRequest req)
  {
    if (!HasAiAccess) return Forbid();
    var ctx = req.Context is not null ? $"\nContexto do material:\n{req.Context[..Math.Min(5000, req.Context.Length)]}" : "";
    var prompt = $$"""
            Avalie a resposta do aluno e retorne APENAS o JSON com três campos:
            {
              "correct": true ou false,
              "feedback": "explicação clara, encorajadora e didática para o aluno em português",
              "teacherExplanation": "análise técnica pedagógica para o professor: nível cognitivo da Taxonomia de Bloom demonstrado, pontos fortes, lacunas conceituais e critérios usados para a nota"
            }

            Pergunta: {{req.Question}}
            Resposta do aluno: {{req.Answer}}
            {{ctx}}
            """;

    var raw = await claude.CompleteAsync(NeuroPersona.Evaluator, prompt, 800);
    var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
    if (start == -1 || end == -1) return StatusCode(500, new { error = "Resposta inválida da IA." });

    return Ok(JsonDocument.Parse(raw[start..(end + 1)]).RootElement);
  }

  /// <summary>Registra uma tentativa de exercício do aluno.</summary>
  [HttpPost("attempts")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> RecordAttempt(RecordAttemptRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var xp = req.IsCorrect ? 50 : 10;

    var attempt = new ExerciseAttempt
    {
      UserId = userId,
      LessonId = req.LessonId ?? Guid.Empty,
      ModuleId = req.ModuleId,
      Question = req.Question,
      Answer = req.Answer,
      IsCorrect = req.IsCorrect,
      Feedback = req.Feedback,
      TeacherExplanation = req.TeacherExplanation,
      ReviewStatus = req.PendingReview ? ReviewStatus.PendingReview : ReviewStatus.AutoApproved,
      XpGained = xp,
    };
    db.ExerciseAttempts.Add(attempt);
    await db.SaveChangesAsync();

    return Ok(new { id = attempt.Id, xpGained = xp });
  }

  /// <summary>Lista as tentativas de exercícios do aluno autenticado.</summary>
  [HttpGet("attempts")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> GetAttempts([FromQuery] int limit = 20)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var attempts = await db.ExerciseAttempts
        .Where(a => a.UserId == userId)
        .OrderByDescending(a => a.CreatedAt)
        .Take(limit)
        .ToListAsync();

    return Ok(attempts.Select(a => new
    {
      a.Id,
      a.Question,
      a.Answer,
      a.IsCorrect,
      a.Feedback,
      a.XpGained,
      a.LessonId,
      a.ModuleId,
      createdAt = a.CreatedAt,
      reviewStatus = a.ReviewStatus.ToString().ToLower()
    }));
  }

  // ── Professor review endpoints ───────────────────────────────────────────

  /// <summary>Lista as tentativas pendentes de revisão do professor.</summary>
  [HttpGet("attempts/pending-review")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> GetPendingReviews()
  {
    var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Get all student IDs enrolled in this teacher's classes
    var studentIds = await db.Classes
        .Where(c => c.TeacherId == teacherId)
        .SelectMany(c => c.Students.Select(s => s.UserId))
        .Distinct()
        .ToListAsync();

    var attempts = await db.ExerciseAttempts
        .Include(a => a.User)
        .Where(a => studentIds.Contains(a.UserId) && a.ReviewStatus == ReviewStatus.PendingReview)
        .OrderByDescending(a => a.CreatedAt)
        .Take(100)
        .ToListAsync();

    return Ok(attempts.Select(a => new
    {
      a.Id,
      a.Question,
      a.Answer,
      a.IsCorrect,
      a.Feedback,
      a.TeacherExplanation,
      a.XpGained,
      a.CreatedAt,
      studentName = a.User.Name,
      studentEmail = a.User.Email,
    }));
  }

  /// <summary>Professor revisa uma tentativa (aceita ou rejeita).</summary>
  [HttpPut("attempts/{id:guid}/review")]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> ReviewAttempt(Guid id, ReviewAttemptRequest req)
  {
    var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var attempt = await db.ExerciseAttempts.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
    if (attempt is null) return NotFound();

    // Verify the attempt belongs to a student in this teacher's class
    var isTeachersStudent = await db.Classes
        .Where(c => c.TeacherId == teacherId)
        .AnyAsync(c => c.Students.Any(s => s.UserId == attempt.UserId));
    if (!isTeachersStudent) return Forbid();

    if (req.Status == "accepted")
    {
      attempt.ReviewStatus = ReviewStatus.Accepted;
    }
    else if (req.Status == "rejected")
    {
      attempt.ReviewStatus = ReviewStatus.Rejected;
      attempt.IsCorrect = false;
      attempt.XpGained = 10;
      if (req.Note is not null)
        attempt.Feedback = req.Note;
    }
    else
    {
      return BadRequest(new { error = "Status inválido. Use 'accepted' ou 'rejected'." });
    }

    await db.SaveChangesAsync();
    return Ok(new { id = attempt.Id, reviewStatus = attempt.ReviewStatus.ToString().ToLower() });
  }
}
