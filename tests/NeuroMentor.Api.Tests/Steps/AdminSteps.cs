using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class AdminSteps(TestWorld world)
{
    private User? _alvo;

    private AdminController NewController() => world.Authenticate(new AdminController(world.Db));

    // ── Given ────────────────────────────────────────────────────────────────

    [Given(@"um administrador autenticado")]
    public void DadoAdmin() =>
        world.CurrentUser = world.AddUser("admin@escola.com", "senha123", UserRole.Teacher, aiEnabled: true, isAdmin: true, name: "Admin");

    [Given(@"existe um usuário comum ""(.*)""")]
    public void DadoUsuarioComum(string email) =>
        _alvo = world.AddUser(email, "senha123", UserRole.Student, name: "Comum");

    [Given(@"esse usuário possui uma aula ""(.*)""")]
    public void DadoUsuarioPossuiAula(string titulo)
    {
        world.Db.Lessons.Add(new Lesson { Title = titulo, SourceFileName = "a.txt", RawText = "texto", TeacherId = _alvo!.Id });
        world.Db.SaveChanges();
    }

    // ── When ─────────────────────────────────────────────────────────────────

    [When(@"o admin lista os usuários")]
    public async Task QuandoListaUsuarios() => world.Result = await NewController().GetUsers(null);

    [When(@"o admin cria o administrador ""(.*)"" e-mail ""(.*)"" senha ""(.*)""")]
    public async Task QuandoCriaAdmin(string nome, string email, string senha) =>
        world.Result = await NewController().CreateAdmin(new CreateAdminRequest(nome, email, senha));

    [When(@"o admin habilita o acesso à IA desse usuário")]
    public async Task QuandoHabilitaIa() =>
        world.Result = await NewController().SetAiAccess(_alvo!.Id, new SetAiAccessRequest(true));

    [When(@"o admin tenta deletar a própria conta")]
    public async Task QuandoDeletaPropria() =>
        world.Result = await NewController().DeleteUser(world.CurrentUser!.Id);

    [When(@"o admin deleta esse usuário")]
    public async Task QuandoDeletaUsuario() =>
        world.Result = await NewController().DeleteUser(_alvo!.Id);

    [When(@"o admin lista os materiais")]
    public async Task QuandoListaMateriais() => world.Result = await NewController().GetLessons();

    // ── Then ─────────────────────────────────────────────────────────────────

    [Then(@"a lista deve conter pelo menos (\d+) usuários")]
    public void EntaoListaUsuarios(int min) => Assert.True(TestWorld.AsList(world.LastValue).Count >= min);

    [Then(@"a lista deve conter pelo menos (\d+) material")]
    public void EntaoListaMateriais(int min) => Assert.True(TestWorld.AsList(world.LastValue).Count >= min);

    [Then(@"o usuário deve ficar com acesso à IA habilitado")]
    public void EntaoUsuarioComIa()
    {
        var u = world.Db.Users.Find(_alvo!.Id)!;
        Assert.True(u.IsAiEnabled);
    }
}
