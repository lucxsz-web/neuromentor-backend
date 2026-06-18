using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.DTOs.Classes;
using NeuroMentor.Api.Models;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public class ClassesController(AppDbContext db) : ControllerBase
{
  private static string GenerateCode()
  {
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
  }

  // Teacher: list own classes
  /// <summary>Lista as turmas do professor autenticado.</summary>
  [HttpGet]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> List()
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var classes = await db.Classes
        .Where(c => c.TeacherId == userId)
        .Include(c => c.ClassLessons).ThenInclude(cl => cl.Lesson)
        .Include(c => c.Students).ThenInclude(cs => cs.User)
        .Include(c => c.Teacher)
        .OrderByDescending(c => c.CreatedAt)
        .ToListAsync();

    return Ok(classes.Select(MapClass));
  }

  // Teacher: create class
  /// <summary>Cria uma nova turma (somente professor).</summary>
  [HttpPost]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(403)]
  public async Task<IActionResult> Create(CreateClassRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var teacher = await db.Users.FindAsync(userId);
    if (teacher?.Role != UserRole.Teacher) return Forbid();

    string code;
    do { code = GenerateCode(); } while (await db.Classes.AnyAsync(c => c.Code == code));

    var cls = new ClassRoom { Name = req.Name.Trim(), Code = code, TeacherId = userId };
    db.Classes.Add(cls);
    await db.SaveChangesAsync();

    var created = await db.Classes
        .Include(c => c.ClassLessons).ThenInclude(cl => cl.Lesson)
        .Include(c => c.Students).ThenInclude(cs => cs.User)
        .Include(c => c.Teacher)
        .FirstAsync(c => c.Id == cls.Id);
    return Ok(MapClass(created));
  }

  // Teacher: delete class
  /// <summary>Exclui uma turma do professor autenticado.</summary>
  [HttpDelete("{id:guid}")]
  [ProducesResponseType(204)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> Delete(Guid id)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == userId);
    if (cls is null) return NotFound();
    db.Classes.Remove(cls);
    await db.SaveChangesAsync();
    return NoContent();
  }

  // Teacher: add lesson to class
  /// <summary>Adiciona uma aula a uma turma.</summary>
  [HttpPost("{id:guid}/lessons")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  [ProducesResponseType(409)]
  public async Task<IActionResult> AddLesson(Guid id, AddLessonToClassRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == userId);
    if (cls is null) return NotFound();

    if (await db.ClassLessons.AnyAsync(cl => cl.ClassRoomId == id && cl.LessonId == req.LessonId))
      return Conflict(new { error = "Aula já está nessa turma." });

    db.ClassLessons.Add(new ClassLesson { ClassRoomId = id, LessonId = req.LessonId });
    await db.SaveChangesAsync();
    return Ok();
  }

  // Teacher: remove lesson from class
  /// <summary>Remove uma aula de uma turma.</summary>
  [HttpDelete("{id:guid}/lessons/{lessonId:guid}")]
  [ProducesResponseType(204)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> RemoveLesson(Guid id, Guid lessonId)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == userId);
    if (cls is null) return NotFound();

    var cl = await db.ClassLessons.FindAsync(id, lessonId);
    if (cl is null) return NotFound();
    db.ClassLessons.Remove(cl);
    await db.SaveChangesAsync();
    return NoContent();
  }

  // Student: join class by code
  /// <summary>Aluno ingressa em uma turma pelo código de acesso.</summary>
  [HttpPost("join")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  [ProducesResponseType(409)]
  public async Task<IActionResult> Join(JoinClassRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var cls = await db.Classes.FirstOrDefaultAsync(c => c.Code == req.Code.Trim().ToUpper());
    if (cls is null) return NotFound(new { error = "Turma não encontrada." });

    if (await db.ClassStudents.AnyAsync(cs => cs.ClassRoomId == cls.Id && cs.UserId == userId))
      return Conflict(new { error = "Você já está nessa turma." });

    db.ClassStudents.Add(new ClassStudent { ClassRoomId = cls.Id, UserId = userId });
    await db.SaveChangesAsync();

    var joined = await db.Classes
        .Include(c => c.ClassLessons).ThenInclude(cl => cl.Lesson)
        .Include(c => c.Students).ThenInclude(cs => cs.User)
        .Include(c => c.Teacher)
        .FirstAsync(c => c.Id == cls.Id);
    return Ok(MapClass(joined));
  }

  // Student: get my enrolled classes
  /// <summary>Lista as turmas em que o aluno está matriculado.</summary>
  [HttpGet("my")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> MyClasses()
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var classes = await db.Classes
        .Where(c => c.Students.Any(s => s.UserId == userId))
        .Include(c => c.ClassLessons).ThenInclude(cl => cl.Lesson)
        .Include(c => c.Students).ThenInclude(cs => cs.User)
        .Include(c => c.Teacher)
        .ToListAsync();

    return Ok(classes.Select(MapClass));
  }

  // Student: get lesson IDs from enrolled classes
  /// <summary>Retorna os IDs das aulas vinculadas às turmas do aluno.</summary>
  [HttpGet("my/lesson-ids")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> MyLessonIds()
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var ids = await db.ClassLessons
        .Where(cl => cl.ClassRoom.Students.Any(s => s.UserId == userId))
        .Select(cl => cl.LessonId)
        .Distinct()
        .ToListAsync();

    return Ok(ids);
  }

  private static ClassDto MapClass(ClassRoom c) => new(
      c.Id, c.Name, c.Code, c.Teacher?.Name ?? "",
      c.CreatedAt,
      c.ClassLessons.Select(cl => new ClassLessonDto(cl.LessonId, cl.Lesson?.Title ?? "")).ToList(),
      c.Students.Select(cs => new ClassStudentDto(cs.UserId, cs.User?.Name ?? "", cs.User?.Email ?? "", cs.JoinedAt)).ToList()
  );
}
