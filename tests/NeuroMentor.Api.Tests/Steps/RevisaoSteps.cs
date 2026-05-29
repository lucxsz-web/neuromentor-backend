using System.Text.Json;
using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class RevisaoSteps(TestWorld world)
{
    private ReviewController NewController() =>
        world.Authenticate(new ReviewController(world.Db, world.Claude));

    [Given(@"a IA responderá com um guia de revisão")]
    public void DadoIaResponderaGuia() =>
        world.ClaudeHandler.ResponseText =
            """
            { "topics": [ { "title": "Fotossíntese", "explanation": "Revise o processo.", "tips": ["estude a clorofila"] } ],
              "summary": "Reforce os conceitos de produção de energia nas plantas." }
            """;

    [When(@"o aluno solicita revisão das questões que errou")]
    public async Task QuandoSolicitaRevisao()
    {
        var req = new GenerateReviewRequest(
            LessonId: null,
            Context: "Material sobre fotossíntese.",
            WrongAnswers: ["O que é fotossíntese?", "Onde ocorre?"]);
        world.Result = await NewController().Generate(req);
    }

    [Then(@"o guia deve conter um resumo")]
    public void EntaoGuiaContemResumo()
    {
        var json = Assert.IsType<JsonElement>(world.LastValue);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("summary").GetString()));
    }
}
