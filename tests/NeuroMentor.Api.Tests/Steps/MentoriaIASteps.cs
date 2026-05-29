using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class MentoriaIASteps(TestWorld world)
{
    private Guid? _moduleId;

    private ChatController NewController() =>
        world.Authenticate(new ChatController(world.Claude, world.Db));

    [Given(@"um módulo ""(.*)"" com o trecho de material ""(.*)""")]
    public void DadoModuloComTrecho(string titulo, string trecho)
    {
        var lesson = new Lesson { Title = "Aula", SourceFileName = "a.txt", RawText = trecho, TeacherId = Guid.NewGuid() };
        var module = new LessonModule
        {
            Title = titulo,
            Summary = "resumo",
            Concepts = ["clorofila"],
            TextChunk = trecho,
            LessonId = lesson.Id,
        };
        lesson.Modules.Add(module);
        world.Db.Lessons.Add(lesson);
        world.Db.SaveChanges();
        _moduleId = module.Id;
    }

    [Given(@"a IA responderá com exercícios válidos")]
    public void DadoIaResponderaExercicios() =>
        world.ClaudeHandler.ResponseText =
            """{ "exercises": [ { "id": "ex-1", "question": "O que é fotossíntese?", "type": "open" } ] }""";

    [When(@"o aluno solicita exercícios de mentoria sobre esse módulo")]
    public async Task QuandoSolicitaComModulo()
    {
        var req = new ChatRequest(
            Messages: [new ChatMessage("user", "Gere exercícios sobre o módulo.")],
            Context: null,
            ModuleId: _moduleId);
        world.Result = await NewController().GenerateExercises(req);
    }

    [When(@"o aluno solicita exercícios de mentoria sem módulo")]
    public async Task QuandoSolicitaSemModulo()
    {
        var req = new ChatRequest(
            Messages: [new ChatMessage("user", "Gere exercícios.")],
            Context: null,
            ModuleId: null);
        world.Result = await NewController().GenerateExercises(req);
    }

    [Then(@"o prompt enviado à IA deve conter ""(.*)""")]
    public void EntaoPromptContem(string esperado)
    {
        Assert.NotNull(world.ClaudeHandler.LastSystemPrompt);
        Assert.Contains(esperado, world.ClaudeHandler.LastSystemPrompt!);
    }
}
