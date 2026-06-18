using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NeuroMentor.Api.Data;
using NeuroMentor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Parse Railway's DATABASE_URL (postgres://user:pass@host:port/db) into Npgsql format
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
  var uri = new Uri(databaseUrl);
  var userInfo = uri.UserInfo.Split(':');
  var npgsqlConn = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
  builder.Configuration["ConnectionStrings:Default"] = npgsqlConn;
}

// ── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// ── Auth (JWT) ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
      var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
      opt.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
      };
    });
builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────────────────────
var aiProvider = builder.Configuration["AI:Provider"]?.ToLower() ?? "anthropic";
if (aiProvider == "openai")
  builder.Services.AddHttpClient<OpenAiService>();
else
  builder.Services.AddHttpClient<ClaudeService>();

builder.Services.AddScoped<IAiService>(sp =>
    aiProvider == "openai"
        ? sp.GetRequiredService<OpenAiService>()
        : sp.GetRequiredService<ClaudeService>());

builder.Services.AddSingleton<TextExtractionService>();

// ── Controllers + Swagger ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
  opt.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "NeuroMentor API",
    Version = "v1",
    Description = "API da plataforma NeuroMentor — tutoria adaptativa com IA.",
  });

  var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
  var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
  opt.IncludeXmlComments(xmlPath);

  // Configura autenticação JWT no Swagger UI
  opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Insira o token JWT. Exemplo: eyJhbGciOiJIUzI1NiIs...",
  });

  opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(origin =>
    {
      var uri = new Uri(origin);
      // Allow configured origins + all Vercel preview deployments
      return allowedOrigins.Contains(origin) ||
             uri.Host.EndsWith(".vercel.app") ||
             uri.Host == "localhost" ||
             uri.Host.StartsWith("localhost:");
    })
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ── App pipeline ──────────────────────────────────────────────────────────
var app = builder.Build();

// Auto-migrate + seed admin on startup
using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
  db.Database.Migrate();

  const string adminEmail = "icaro.costa@gmail.com";
  if (!db.Users.Any(u => u.Email == adminEmail))
  {
    db.Users.Add(new NeuroMentor.Api.Models.User
    {
      Name = "Ícaro Costa",
      Email = adminEmail,
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("13052891idI"),
      Role = NeuroMentor.Api.Models.UserRole.Teacher,
      IsAiEnabled = true,
      IsAdmin = true,
    });
    db.SaveChanges();
  }
  else
  {
    var admin = db.Users.First(u => u.Email == adminEmail);
    if (!admin.IsAdmin || !admin.IsAiEnabled)
    {
      admin.IsAdmin = true;
      admin.IsAiEnabled = true;
      db.SaveChanges();
    }
  }
}

app.UseCors();

// Swagger UI (disponível em qualquer ambiente para facilitar desenvolvimento)
app.UseSwagger();
app.UseSwaggerUI(opt =>
{
  opt.SwaggerEndpoint("/swagger/v1/swagger.json", "NeuroMentor API v1");
  opt.DocumentTitle = "NeuroMentor API Docs";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/health", () => new { status = "ok", version = "1.0.0" });

app.Run();
