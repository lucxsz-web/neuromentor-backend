using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Classes;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class TurmasSteps(TestWorld world)
{
    private ClassRoom? _turma;
    private Lesson? _aula;

    private ClassesController NewController() =>
        world.Authenticate(new ClassesController(world.Db));

    // ── Given ────────────────────────────────────────────────────────────────

    [Given(@"o professor já possui uma turma ""(.*)""")]
    public void DadoProfessorPossuiTurma(string nome)
    {
        _turma = new ClassRoom { Name = nome, Code = "OWN111", TeacherId = world.CurrentUser!.Id };
        world.Db.Classes.Add(_turma);
        world.Db.SaveChanges();
    }

    [Given(@"existe uma turma ""(.*)"" com o código ""(.*)""")]
    public void DadoExisteTurmaComCodigo(string nome, string codigo)
    {
        var teacher = world.AddUser($"dono.{codigo}@escola.com", "senha123", UserRole.Teacher, name: "Dono");
        _turma = new ClassRoom { Name = nome, Code = codigo, TeacherId = teacher.Id };
        world.Db.Classes.Add(_turma);
        world.Db.SaveChanges();
    }

    [Given(@"o aluno já está matriculado nessa turma")]
    public void DadoAlunoMatriculado()
    {
        world.Db.ClassStudents.Add(new ClassStudent { ClassRoomId = _turma!.Id, UserId = world.CurrentUser!.Id });
        world.Db.SaveChanges();
    }

    [Given(@"o professor possui uma aula ""(.*)""")]
    public void DadoProfessorPossuiAula(string titulo)
    {
        _aula = new Lesson { Title = titulo, SourceFileName = "a.txt", RawText = "texto", TeacherId = world.CurrentUser!.Id };
        world.Db.Lessons.Add(_aula);
        world.Db.SaveChanges();
    }

    [Given(@"a aula já está vinculada à turma")]
    public void DadoAulaVinculada()
    {
        world.Db.ClassLessons.Add(new ClassLesson { ClassRoomId = _turma!.Id, LessonId = _aula!.Id });
        world.Db.SaveChanges();
    }

    // ── When ─────────────────────────────────────────────────────────────────

    [When(@"o professor cria a turma ""(.*)""")]
    public async Task QuandoCriaTurma(string nome)
    {
        world.Result = await NewController().Create(new CreateClassRequest(nome));
    }

    [When(@"o professor lista suas turmas")]
    public async Task QuandoListaTurmas() => world.Result = await NewController().List();

    [When(@"o aluno entra na turma com o código ""(.*)""")]
    public async Task QuandoEntraNaTurma(string codigo) =>
        world.Result = await NewController().Join(new JoinClassRequest(codigo));

    [When(@"o professor adiciona a aula à turma")]
    public async Task QuandoAdicionaAula() =>
        world.Result = await NewController().AddLesson(_turma!.Id, new AddLessonToClassRequest(_aula!.Id, _aula.Title));

    [When(@"o professor remove a turma")]
    public async Task QuandoRemoveTurma() => world.Result = await NewController().Delete(_turma!.Id);

    [When(@"o aluno consulta suas turmas matriculadas")]
    public async Task QuandoConsultaMinhasTurmas() => world.Result = await NewController().MyClasses();

    // ── Then ─────────────────────────────────────────────────────────────────

    [Then(@"a turma criada deve ter um código de (\d+) caracteres")]
    public void EntaoCodigoComTamanho(int tamanho)
    {
        var dto = Assert.IsType<ClassDto>(world.LastValue);
        Assert.Equal(tamanho, dto.Code.Length);
    }

    [Then(@"deve existir (\d+) turma persistida")]
    public void EntaoTurmasPersistidas(int qtd) => Assert.Equal(qtd, world.Db.Classes.Count());

    [Then(@"a lista deve conter (\d+) turma")]
    public void EntaoListaContem(int qtd) => Assert.Equal(qtd, TestWorld.AsList(world.LastValue).Count);
}
