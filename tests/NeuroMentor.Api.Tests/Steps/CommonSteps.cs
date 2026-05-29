using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

/// <summary>
/// Steps compartilhados por várias features: autenticação de papéis e a asserção de status.
/// Definidos uma única vez para evitar ambiguidade de binding no Reqnroll.
/// </summary>
[Binding]
public class CommonSteps(TestWorld world)
{
    [Given(@"um professor autenticado com acesso à IA")]
    public void DadoProfessorComIa() =>
        world.CurrentUser = world.AddUser("prof.ia@escola.com", "senha123", UserRole.Teacher, aiEnabled: true, name: "Professor IA");

    [Given(@"um professor autenticado sem acesso à IA")]
    public void DadoProfessorSemIa() =>
        world.CurrentUser = world.AddUser("prof.semia@escola.com", "senha123", UserRole.Teacher, aiEnabled: false, name: "Professor Sem IA");

    [Given(@"um aluno autenticado com acesso à IA")]
    public void DadoAlunoComIa() =>
        world.CurrentUser = world.AddUser("aluno.ia@escola.com", "senha123", UserRole.Student, aiEnabled: true, name: "Aluno IA");

    [Given(@"um aluno autenticado sem acesso à IA")]
    public void DadoAlunoSemIa() =>
        world.CurrentUser = world.AddUser("aluno.semia@escola.com", "senha123", UserRole.Student, aiEnabled: false, name: "Aluno Sem IA");

    [Given(@"um aluno autenticado")]
    public void DadoAlunoAutenticado() =>
        world.CurrentUser = world.AddUser("aluno@escola.com", "senha123", UserRole.Student, aiEnabled: false, name: "Aluno");

    [Then(@"a resposta deve ter status (\d+)")]
    public void EntaoStatus(int esperado) => Assert.Equal(esperado, world.LastStatus);
}
