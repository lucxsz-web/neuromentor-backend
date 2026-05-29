using Microsoft.EntityFrameworkCore;
using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Lessons;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class ProcessamentoRagSteps(TestWorld world)
{
    private Lesson _lesson = null!;
    private LessonModule? _module;

    private LessonsController NewController() =>
        world.Authenticate(new LessonsController(world.Db, world.Claude, world.Extractor));

    [Given(@"uma aula com o texto ""(.*)""")]
    public void DadoUmaAulaComTexto(string texto)
    {
        _lesson = new Lesson
        {
            Title = "Aula de Teste",
            SourceFileName = "aula.txt",
            RawText = texto,
            TeacherId = world.CurrentUser!.Id,
        };
        world.Db.Lessons.Add(_lesson);
        world.Db.SaveChanges();
    }

    [Given(@"a IA responderá com (\d+) módulos de aprendizagem")]
    public void DadoIaResponderaComModulos(int quantidade)
    {
        var modulos = Enumerable.Range(1, quantidade).Select(i =>
            $$"""
            { "id": "mod-{{i}}", "title": "Módulo {{i}}", "summary": "Resumo do módulo {{i}}.", "concepts": ["conceito{{i}}"], "match": 0.9 }
            """);
        world.ClaudeHandler.ResponseText = $$"""
            { "modules": [ {{string.Join(",", modulos)}} ] }
            """;
    }

    [Given(@"uma aula com um módulo pendente")]
    public void DadoAulaComModuloPendente()
    {
        // Garante um professor dono caso a feature não tenha definido um.
        world.CurrentUser ??= world.AddUser("prof.rag@escola.com", "senha123", UserRole.Teacher, aiEnabled: true);

        _lesson = new Lesson { Title = "Aula", SourceFileName = "a.txt", RawText = "texto", TeacherId = world.CurrentUser.Id };
        _module = new LessonModule { Title = "Módulo 1", Summary = "resumo", Status = ModuleStatus.Pending, LessonId = _lesson.Id };
        _lesson.Modules.Add(_module);
        world.Db.Lessons.Add(_lesson);
        world.Db.SaveChanges();
    }

    [When(@"solicito a geração de módulos para a aula")]
    public async Task QuandoGeroModulos()
    {
        world.Result = await NewController().Generate(
            new GenerateModulesRequest(_lesson.Id, _lesson.RawText, _lesson.Title));
    }

    [When(@"defino o status do módulo como ""(.*)""")]
    public async Task QuandoDefinoStatus(string status)
    {
        world.Result = await NewController().SetModuleStatus(
            _lesson.Id, _module!.Id, new SetModuleStatusRequest(status));
    }

    [Then(@"devem existir (\d+) módulos persistidos para a aula")]
    public async Task EntaoExistemModulos(int quantidade)
    {
        var count = await world.Db.LessonModules.CountAsync(m => m.LessonId == _lesson.Id);
        Assert.Equal(quantidade, count);
    }

    [Then(@"todos os módulos devem estar com status ""(.*)""")]
    public async Task EntaoTodosComStatus(string status)
    {
        var modulos = await world.Db.LessonModules.Where(m => m.LessonId == _lesson.Id).ToListAsync();
        Assert.All(modulos, m => Assert.Equal(status, m.Status.ToString().ToLower()));
    }

    [Then(@"o módulo deve estar com status ""(.*)""")]
    public async Task EntaoModuloComStatus(string status)
    {
        var m = await world.Db.LessonModules.FindAsync(_module!.Id);
        Assert.Equal(status, m!.Status.ToString().ToLower());
    }
}
