using System.IdentityModel.Tokens.Jwt;
using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Auth;
using NeuroMentor.Api.Models;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class AutenticacaoSteps(TestWorld world)
{
    private User? _lastUser;
    private string? _token;

    private AuthController NewController() => world.Authenticate(new AuthController(world.Db, world.Jwt));

    // ── Given ────────────────────────────────────────────────────────────────

    [Given(@"um usuário cadastrado com e-mail ""(.*)"" e senha ""(.*)""")]
    public void DadoUsuarioCadastrado(string email, string senha) =>
        _lastUser = world.AddUser(email, senha);

    [Given(@"um usuário cadastrado com e-mail ""(.*)"" e senha ""(.*)"" com acesso à IA")]
    public void DadoUsuarioCadastradoComIa(string email, string senha) =>
        _lastUser = world.AddUser(email, senha, aiEnabled: true);

    [Given(@"um usuário autenticado com e-mail ""(.*)"" e senha ""(.*)""")]
    public void DadoUsuarioAutenticado(string email, string senha)
    {
        _lastUser = world.AddUser(email, senha);
        world.CurrentUser = _lastUser;
    }

    // ── When ─────────────────────────────────────────────────────────────────

    [When(@"registro o usuário ""(.*)"" e-mail ""(.*)"" senha ""(.*)"" papel ""(.*)""")]
    public async Task QuandoRegistro(string nome, string email, string senha, string papel)
    {
        world.Result = await NewController().Register(new RegisterRequest(nome, email, senha, papel));
    }

    [When(@"faço login com e-mail ""(.*)"" e senha ""(.*)""")]
    public async Task QuandoLogin(string email, string senha)
    {
        world.Result = await NewController().Login(new LoginRequest(email, senha));
    }

    [When(@"troco a senha atual ""(.*)"" pela nova senha ""(.*)""")]
    public async Task QuandoTrocoSenha(string atual, string nova)
    {
        world.Result = await NewController().ChangePassword(new ChangePasswordRequest(atual, nova));
    }

    [When(@"gero um token JWT para esse usuário")]
    public void QuandoGeroToken() => _token = world.Jwt.Generate(_lastUser!);

    // ── Then ─────────────────────────────────────────────────────────────────

    [Then(@"a resposta deve conter um token JWT")]
    public void EntaoContemToken()
    {
        var resp = Assert.IsType<AuthResponse>(world.LastValue);
        Assert.False(string.IsNullOrWhiteSpace(resp.Token));
    }

    [Then(@"o token deve conter a claim ""(.*)"" igual a ""(.*)""")]
    public void EntaoTokenContemClaim(string claim, string valor)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_token);
        Assert.Equal(valor, jwt.Claims.First(c => c.Type == claim).Value);
    }
}
