using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class DashboardSteps(TestWorld world)
{
    private ExercisesController NewController() =>
        world.Authenticate(new ExercisesController(world.Db, world.Claude));

    [Given(@"o aluno possui as seguintes tentativas:")]
    public void DadoTentativas(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            world.Db.ExerciseAttempts.Add(new ExerciseAttempt
            {
                UserId = world.CurrentUser!.Id,
                ModuleId = "mod-1",
                Question = "P", Answer = "R",
                IsCorrect = bool.Parse(row["correta"]),
                XpGained = int.Parse(row["xp"]),
                Feedback = "ok",
            });
        }
        world.Db.SaveChanges();
    }

    [When(@"o aluno consulta seu histórico de atividades")]
    public async Task QuandoConsultaHistorico()
    {
        world.Result = await NewController().GetAttempts();
    }

    [Then(@"o histórico deve conter (\d+) atividades")]
    public void EntaoHistoricoConta(int quantidade) =>
        Assert.Equal(quantidade, TestWorld.AsList(world.LastValue).Count);

    [Then(@"o XP total acumulado deve ser (\d+)")]
    public void EntaoXpTotal(int total)
    {
        var soma = TestWorld.AsList(world.LastValue)
            .Sum(item => Convert.ToInt32(TestWorld.Prop(item, "XpGained")));
        Assert.Equal(total, soma);
    }
}
