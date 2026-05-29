using System.Text.Json;
using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Exercises;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class CorrecaoSteps(TestWorld world)
{
    private Guid _attemptId;

    private ExercisesController NewController() =>
        world.Authenticate(new ExercisesController(world.Db, world.Claude));

    private JsonElement Json => Assert.IsType<JsonElement>(world.LastValue);

    // ── Correção automática (IA) ──────────────────────────────────────────────

    [Given(@"a IA avaliará a resposta como correta com o feedback ""(.*)""")]
    public void DadoIaAvaliaCorreta(string feedback) =>
        world.ClaudeHandler.ResponseText =
            $$"""{ "correct": true, "feedback": "{{feedback}}", "teacherExplanation": "Nível: aplicação." }""";

    [When(@"o aluno envia para correção a pergunta ""(.*)"" com a resposta ""(.*)""")]
    public async Task QuandoEnviaParaCorrecao(string pergunta, string resposta)
    {
        world.Result = await NewController().Correct(new CorrectExerciseRequest(pergunta, resposta, null));
    }

    [Then(@"o resultado deve indicar que a resposta está correta")]
    public void EntaoResultadoCorreto() => Assert.True(Json.GetProperty("correct").GetBoolean());

    [Then(@"o resultado deve conter o feedback ""(.*)""")]
    public void EntaoContemFeedback(string feedback) =>
        Assert.Equal(feedback, Json.GetProperty("feedback").GetString());

    // ── Registro de tentativa / XP ─────────────────────────────────────────────

    [When(@"o aluno registra uma tentativa marcada como correta")]
    public async Task QuandoRegistraCorreta() => await RegistrarTentativa(true);

    [When(@"o aluno registra uma tentativa marcada como incorreta")]
    public async Task QuandoRegistraIncorreta() => await RegistrarTentativa(false);

    private async Task RegistrarTentativa(bool correta)
    {
        var req = new RecordAttemptRequest(
            LessonId: null, ModuleId: "mod-1",
            Question: "Pergunta", Answer: "Resposta",
            IsCorrect: correta, Feedback: "feedback");
        world.Result = await NewController().RecordAttempt(req);
    }

    [Then(@"o XP concedido deve ser (\d+)")]
    public void EntaoXpConcedido(int xp) =>
        Assert.Equal(xp, Convert.ToInt32(TestWorld.Prop(world.LastValue, "xpGained")));

    // ── Revisão pelo professor (transição de estado) ───────────────────────────

    [Given(@"um professor dono de uma turma com um aluno matriculado")]
    public void DadoProfessorComTurmaEAluno()
    {
        var teacher = world.AddUser("prof.turma@escola.com", "senha123", UserRole.Teacher, aiEnabled: true, name: "Prof Turma");
        var student = world.AddUser("aluno.turma@escola.com", "senha123", UserRole.Student, name: "Aluno Turma");

        var turma = new ClassRoom { Name = "Turma A", Code = "ABC123", TeacherId = teacher.Id };
        turma.Students.Add(new ClassStudent { ClassRoomId = turma.Id, UserId = student.Id });
        world.Db.Classes.Add(turma);
        world.Db.SaveChanges();

        world.CurrentUser = teacher;
        // guarda o aluno para a próxima etapa via DbContext (já persistido)
        _studentId = student.Id;
    }

    private Guid _studentId;

    [Given(@"o aluno possui uma tentativa correta pendente de revisão valendo (\d+) XP")]
    public void DadoTentativaPendente(int xp)
    {
        var attempt = new ExerciseAttempt
        {
            UserId = _studentId,
            ModuleId = "mod-1",
            Question = "P", Answer = "R",
            IsCorrect = true,
            XpGained = xp,
            ReviewStatus = ReviewStatus.PendingReview,
            Feedback = "ok",
        };
        world.Db.ExerciseAttempts.Add(attempt);
        world.Db.SaveChanges();
        _attemptId = attempt.Id;
    }

    [When(@"o professor rejeita essa tentativa")]
    public async Task QuandoProfessorRejeita()
    {
        world.Result = await NewController().ReviewAttempt(_attemptId, new ReviewAttemptRequest("rejected"));
    }

    [Then(@"a tentativa deve ficar com status ""(.*)""")]
    public void EntaoTentativaStatus(string status)
    {
        var a = world.Db.ExerciseAttempts.Find(_attemptId)!;
        Assert.Equal(status, a.ReviewStatus.ToString().ToLower());
    }

    [Then(@"a tentativa deve valer (\d+) XP")]
    public void EntaoTentativaXp(int xp)
    {
        var a = world.Db.ExerciseAttempts.Find(_attemptId)!;
        Assert.Equal(xp, a.XpGained);
    }

    [Then(@"a tentativa deve ficar marcada como incorreta")]
    public void EntaoTentativaIncorreta()
    {
        var a = world.Db.ExerciseAttempts.Find(_attemptId)!;
        Assert.False(a.IsCorrect);
    }
}
