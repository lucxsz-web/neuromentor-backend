using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class ExerciciosExtraSteps(TestWorld world)
{
    private ExercisesController NewExercises() =>
        world.Authenticate(new ExercisesController(world.Db, world.Claude));

    private ChatController NewChat() =>
        world.Authenticate(new ChatController(world.Claude, world.Db));

    [When(@"o aluno gera exercícios sobre o módulo ""(.*)"" com o contexto ""(.*)""")]
    public async Task QuandoGeraExerciciosComContexto(string modulo, string contexto) =>
        world.Result = await NewExercises().Generate(new GenerateExercisesRequest(null, modulo, contexto));

    [When(@"o aluno gera exercícios sobre o módulo ""(.*)"" sem contexto")]
    public async Task QuandoGeraExerciciosSemContexto(string modulo) =>
        world.Result = await NewExercises().Generate(new GenerateExercisesRequest(null, modulo, null));

    [When(@"o aluno solicita mentoria com o contexto bruto ""(.*)""")]
    public async Task QuandoMentoriaContextoBruto(string contexto)
    {
        var req = new ChatRequest(
            Messages: [new ChatMessage("user", "Me ajude com isso.")],
            Context: contexto,
            ModuleId: null);
        world.Result = await NewChat().GenerateExercises(req);
    }

    [When(@"o professor consulta as tentativas pendentes de revisão")]
    public async Task QuandoConsultaPendentes() =>
        world.Result = await NewExercises().GetPendingReviews();

    [Then(@"a fila de revisão deve conter (\d+) tentativa")]
    public void EntaoFilaContem(int qtd) => Assert.Equal(qtd, TestWorld.AsList(world.LastValue).Count);
}
