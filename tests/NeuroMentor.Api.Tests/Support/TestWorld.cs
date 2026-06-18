using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Services;

namespace NeuroMentor.Api.Tests.Support;

/// <summary>
/// Estado compartilhado de UM cenário BDD. O Reqnroll cria uma instância por cenário
/// (injeção de dependência via construtor nas classes de steps) e a descarta ao final.
/// Concentra o banco em memória, os serviços reais (Jwt/Extractor), o ClaudeService
/// com handler falso e helpers de autenticação/inspeção de resposta.
/// </summary>
public sealed class TestWorld : IDisposable
{
  public AppDbContext Db { get; }
  public IConfiguration Config { get; }
  public JwtService Jwt { get; }
  public TextExtractionService Extractor { get; } = new();
  public FakeClaudeHandler ClaudeHandler { get; } = new();
  public IAiService Claude { get; }

  /// <summary>Usuário "logado" no cenário corrente (define as claims dos controllers).</summary>
  public User? CurrentUser { get; set; }

  /// <summary>Último IActionResult retornado por um controller no cenário.</summary>
  public IActionResult? Result { get; set; }

  public TestWorld()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"neuromentor-tests-{Guid.NewGuid()}")
        .Options;
    Db = new AppDbContext(options);

    Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["Jwt:Key"] = "chave-de-teste-super-secreta-com-mais-de-32-bytes-1234567890",
          ["Jwt:Issuer"] = "neuromentor-test",
          ["Jwt:Audience"] = "neuromentor-test",
          ["Anthropic:ApiKey"] = "sk-test-key",
        })
        .Build();

    Jwt = new JwtService(Config);
    Claude = new ClaudeService(new HttpClient(ClaudeHandler), Config);
  }

  // ── Autenticação ────────────────────────────────────────────────────────

  public ClaimsPrincipal PrincipalFor(User u) => new(new ClaimsIdentity(new[]
  {
        new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
        new Claim(ClaimTypes.Email, u.Email),
        new Claim(ClaimTypes.Role, u.Role.ToString()),
        new Claim(ClaimTypes.Name, u.Name),
        new Claim("isAiEnabled", u.IsAiEnabled.ToString()),
        new Claim("isAdmin", u.IsAdmin.ToString()),
    }, "TestAuth"));

  /// <summary>Anexa o usuário corrente (ou um principal anônimo) ao ControllerContext.</summary>
  public TController Authenticate<TController>(TController controller) where TController : ControllerBase
  {
    var principal = CurrentUser is null
        ? new ClaimsPrincipal(new ClaimsIdentity())
        : PrincipalFor(CurrentUser);

    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext { User = principal }
    };
    return controller;
  }

  // ── Helpers de domínio ────────────────────────────────────────────────────

  public User AddUser(string email, string password, UserRole role = UserRole.Student,
      bool aiEnabled = false, bool isAdmin = false, string name = "Usuário Teste")
  {
    var user = new User
    {
      Name = name,
      Email = email.ToLower(),
      PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
      Role = role,
      IsAiEnabled = aiEnabled,
      IsAdmin = isAdmin,
    };
    Db.Users.Add(user);
    Db.SaveChanges();
    return user;
  }

  // ── Inspeção de respostas (IActionResult) ──────────────────────────────────

  /// <summary>Traduz qualquer IActionResult no status HTTP correspondente.</summary>
  public static int StatusOf(IActionResult result) => result switch
  {
    OkObjectResult => 200,
    OkResult => 200,
    NoContentResult => 204,
    BadRequestObjectResult => 400,
    BadRequestResult => 400,
    UnauthorizedObjectResult => 401,
    UnauthorizedResult => 401,
    ForbidResult => 403,
    NotFoundObjectResult => 404,
    NotFoundResult => 404,
    ConflictObjectResult => 409,
    UnprocessableEntityObjectResult => 422,
    ObjectResult o => o.StatusCode ?? 200,
    StatusCodeResult s => s.StatusCode,
    _ => throw new InvalidOperationException($"Tipo de resultado não mapeado: {result.GetType().Name}")
  };

  public int LastStatus => Result is null
      ? throw new InvalidOperationException("Nenhum resultado foi capturado.")
      : StatusOf(Result);

  /// <summary>Valor (payload) de um OkObjectResult / ObjectResult.</summary>
  public object? LastValue => Result switch
  {
    ObjectResult o => o.Value,
    _ => null
  };

  /// <summary>Lê uma propriedade por nome de um objeto anônimo via reflexão.</summary>
  public static object? Prop(object? obj, string name) =>
      obj?.GetType().GetProperty(name)?.GetValue(obj);

  /// <summary>Enumera o payload como lista de objetos (para coleções anônimas).</summary>
  public static List<object> AsList(object? value)
  {
    var list = new List<object>();
    if (value is IEnumerable en and not string)
      foreach (var item in en) list.Add(item);
    return list;
  }

  public void Dispose() => Db.Dispose();
}
