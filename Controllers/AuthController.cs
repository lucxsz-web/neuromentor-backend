using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.DTOs.Auth;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
  /// <summary>Registra um novo usuário (aluno ou professor).</summary>
  [HttpPost("register")]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(409)]
  public async Task<IActionResult> Register(RegisterRequest req)
  {
    if (await db.Users.AnyAsync(u => u.Email == req.Email.Trim().ToLower()))
      return Conflict(new { error = "Email já cadastrado." });

    if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
      return BadRequest(new { error = "Role inválido. Use 'Student' ou 'Teacher'." });

    var user = new User
    {
      Name = req.Name.Trim(),
      Email = req.Email.Trim().ToLower(),
      PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
      Role = role,
      IsAiEnabled = role == UserRole.Teacher,
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Ok(BuildResponse(user));
  }

  /// <summary>Autentica o usuário e retorna token JWT.</summary>
  [HttpPost("login")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> Login(LoginRequest req)
  {
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLower());
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
      return Unauthorized(new { error = "Email ou senha inválidos." });

    return Ok(BuildResponse(user));
  }

  /// <summary>Altera a senha do usuário autenticado.</summary>
  [Authorize]
  [HttpPut("change-password")]
  [ProducesResponseType(200)]
  [ProducesResponseType(400)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return NotFound();

    if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
      return BadRequest(new { error = "Senha atual incorreta." });

    if (req.NewPassword.Length < 6)
      return BadRequest(new { error = "A nova senha deve ter pelo menos 6 caracteres." });

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await db.SaveChangesAsync();
    return Ok(new { message = "Senha alterada com sucesso." });
  }

  /// <summary>Atualiza o perfil do usuário autenticado.</summary>
  [Authorize]
  [HttpPut("profile")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req)
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return NotFound();

    user.Name = req.Name.Trim();
    user.PhotoUrl = req.PhotoUrl;
    user.Matricula = req.Matricula;
    user.Subject = req.Subject;
    await db.SaveChangesAsync();

    return Ok(BuildResponse(user));
  }

  /// <summary>Retorna os dados do usuário autenticado.</summary>
  [Authorize]
  [HttpGet("me")]
  [ProducesResponseType(200)]
  [ProducesResponseType(401)]
  [ProducesResponseType(404)]
  public async Task<IActionResult> Me()
  {
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return NotFound();
    return Ok(BuildResponse(user));
  }

  private AuthResponse BuildResponse(User user) => new(
      Token: jwt.Generate(user),
      Id: user.Id.ToString(),
      Name: user.Name,
      Email: user.Email,
      Role: user.Role.ToString().ToLower(),
      PhotoUrl: user.PhotoUrl,
      Matricula: user.Matricula,
      Subject: user.Subject,
      IsAiEnabled: user.IsAiEnabled,
      IsAdmin: user.IsAdmin
  );
}
