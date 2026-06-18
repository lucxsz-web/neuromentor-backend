using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.DTOs.Lessons;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonsController(AppDbContext db, IAiService claude, TextExtractionService extractor) : ControllerBase
{
  private static readonly string ModuleSystemPrompt = NeuroPersona.InstructionalDesigner;
  private bool HasAiAccess => User.FindFirstValue("isAiEnabled") == "True";

  /// <summary>Faz upload de um arquivo (PDF/TXT) e extrai o texto da aula.</summary>
  [HttpPost("upload")]
  [RequestSizeLimit(50 * 1024 * 1024)]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(401)]
  [ProducesResponseType(422)]
  public async Task<IActionResult> Upload(IFormFile file)
  {
    if (file is null || file.Length == 0)
      return BadRequest(new { error = "Arquivo não enviado." });

    var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    await using var stream = file.OpenReadStream();
    var text = extractor.Extract(stream, file.FileName);

    if (string.IsNullOrWhiteSpace(text))
      return UnprocessableEntity(new { error = "Não foi possível extrair texto do arquivo." });

    var lesson = new Lesson
    {
      Title = Path.GetFileNameWithoutExtension(file.FileName),
      SourceFileName = file.FileName,
      RawText = text,
      TeacherId = teacherId,
    };
    db.Lessons.Add(lesson);
    await db.SaveChangesAsync();

    return Ok(new LessonUploadResponse(lesson.Id, lesson.Title, text[..Math.Min(5000, text.Length)], text.Length));
  }

  /// <summary>Gera módulos de aprendizagem via IA a partir do material da aula.</summary>
  [HttpPost("generate")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  [ProducesResponseType(404)]
  [ProducesResponseType(500)]
  public async Task<IActionResult> Generate(GenerateModulesRequest req)
  {
    if (!HasAiAccess) return Forbid();
    var lesson = await db.Lessons.Include(l => l.Modules).FirstOrDefaultAsync(l => l.Id == req.LessonId);
    if (lesson is null) return NotFound(new { error = "Aula não encontrada." });

    // Prefer stored full text over the (possibly trimmed) req.Text
    var source = lesson.RawText.Length > req.Text.Length ? lesson.RawText : req.Text;
    var text = source[..Math.Min(15000, source.Length)];
    var prompt = $$"""
            Analise o seguinte material educacional e retorne um JSON com módulos de aprendizagem.
            Título: {{req.Title}}
            Material:
            {{text}}

            Retorne APENAS o JSON, sem texto adicional:
            {
              "modules": [
                {
                  "id": "mod-1",
                  "title": "Título do módulo",
                  "summary": "Resumo de 2-3 frases",
                  "concepts": ["conceito1", "conceito2"],
                  "match": 0.95
                }
              ]
            }
            Gere entre 3 e 6 módulos cobrindo os principais tópicos do material.
            """;

    var raw = await claude.CompleteAsync(ModuleSystemPrompt, prompt, 2000);

    var start = raw.IndexOf('{');
    var end = raw.LastIndexOf('}');
    if (start == -1 || end == -1) return StatusCode(500, new { error = "Resposta inválida da IA." });

    using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
    var modulesEl = doc.RootElement.GetProperty("modules");

    // Remove old pending modules
    db.LessonModules.RemoveRange(lesson.Modules.Where(m => m.Status == ModuleStatus.Pending));

    var newModules = new List<LessonModule>();
    var order = 0;
    foreach (var m in modulesEl.EnumerateArray())
    {
      var concepts = m.GetProperty("concepts").EnumerateArray()
          .Select(c => c.GetString() ?? "").ToList();

      var title = m.GetProperty("title").GetString() ?? "";
      var summary = m.GetProperty("summary").GetString() ?? "";
      var chunk = TextExtractionService.ExtractChunk(lesson.RawText, title, concepts);

      newModules.Add(new LessonModule
      {
        LessonId = lesson.Id,
        Title = title,
        Summary = summary,
        Concepts = concepts,
        MatchScore = m.GetProperty("match").GetDouble(),
        Order = order++,
        TextChunk = chunk,
      });
    }

    db.LessonModules.AddRange(newModules);
    await db.SaveChangesAsync();

    return Ok(new { modules = newModules.Select(MapModule) });
  }

  /// <summary>Lista todas as aulas do professor autenticado.</summary>
  [HttpGet]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> List()
  {
    var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var lessons = await db.Lessons
        .Where(l => l.TeacherId == teacherId)
        .Include(l => l.Modules)
        .OrderByDescending(l => l.CreatedAt)
        .ToListAsync();

    return Ok(lessons.Select(MapLesson));
  }

  /// <summary>Lista as aulas disponíveis para o aluno (via turmas matriculadas).</summary>
  [HttpGet("available")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> Available()
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var lessonIds = await db.Classes
        .Where(c => c.Students.Any(s => s.UserId == userId))
        .SelectMany(c => c.ClassLessons.Select(cl => cl.LessonId))
        .Distinct()
        .ToListAsync();

    var lessons = await db.Lessons
        .Where(l => lessonIds.Contains(l.Id))
        .Include(l => l.Modules)
        .OrderByDescending(l => l.CreatedAt)
        .ToListAsync();

    return Ok(lessons.Select(MapLesson));
  }

  /// <summary>Retorna os detalhes de uma aula pelo ID.</summary>
  [HttpGet("{id:guid}")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> Get(Guid id)
  {
    var lesson = await db.Lessons.Include(l => l.Modules).FirstOrDefaultAsync(l => l.Id == id);
    if (lesson is null) return NotFound();
    return Ok(MapLesson(lesson));
  }

  /// <summary>Altera o status de um módulo (Pending, Approved, Rejected).</summary>
  [HttpPut("{lessonId:guid}/modules/{moduleId:guid}/status")]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> SetModuleStatus(Guid lessonId, Guid moduleId, SetModuleStatusRequest req)
  {
    var module = await db.LessonModules.FirstOrDefaultAsync(m => m.Id == moduleId && m.LessonId == lessonId);
    if (module is null) return NotFound();

    if (!Enum.TryParse<ModuleStatus>(req.Status, ignoreCase: true, out var status))
      return BadRequest(new { error = "Status inválido. Use Pending, Approved ou Rejected." });

    module.Status = status;
    await db.SaveChangesAsync();
    return Ok(MapModule(module));
  }

  /// <summary>Exclui uma aula do professor autenticado.</summary>
  [HttpDelete("{id:guid}")]
  [ProducesResponseType(204)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> Delete(Guid id)
  {
    var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == id && l.TeacherId == teacherId);
    if (lesson is null) return NotFound();
    db.Lessons.Remove(lesson);
    await db.SaveChangesAsync();
    return NoContent();
  }

  private static LessonDto MapLesson(Lesson l) => new(
      l.Id, l.Title, l.SourceFileName, l.CreatedAt,
      l.Modules.OrderBy(m => m.Order).Select(MapModule).ToList()
  );

  private static ModuleDto MapModule(LessonModule m) => new(
      m.Id, m.Title, m.Summary, m.Concepts, m.MatchScore, m.Status.ToString().ToLower(), m.Order
  );
}
