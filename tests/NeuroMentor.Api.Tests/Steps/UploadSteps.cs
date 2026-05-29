using System.Text;
using Microsoft.AspNetCore.Http;
using NeuroMentor.Api.Controllers;
using NeuroMentor.Api.DTOs.Lessons;
using NeuroMentor.Api.Tests.Support;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class UploadSteps(TestWorld world)
{
    private LessonsController NewController() =>
        world.Authenticate(new LessonsController(world.Db, world.Claude, world.Extractor));

    private static IFormFile FileFrom(string nome, string conteudo)
    {
        var bytes = Encoding.UTF8.GetBytes(conteudo);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", nome)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain",
        };
    }

    [When(@"faço upload do arquivo ""(.*)"" com o conteúdo ""(.*)""")]
    public async Task QuandoUploadComConteudo(string nome, string conteudo)
    {
        world.Result = await NewController().Upload(FileFrom(nome, conteudo));
    }

    [When(@"faço upload de um arquivo vazio chamado ""(.*)""")]
    public async Task QuandoUploadVazio(string nome)
    {
        world.Result = await NewController().Upload(FileFrom(nome, ""));
    }

    [Then(@"a aula deve ser persistida com o texto extraído")]
    public void EntaoAulaPersistida()
    {
        var lesson = Assert.Single(world.Db.Lessons);
        Assert.False(string.IsNullOrWhiteSpace(lesson.RawText));
    }

    [Then(@"o tamanho de texto retornado deve ser maior que zero")]
    public void EntaoTamanhoMaiorQueZero()
    {
        var resp = Assert.IsType<LessonUploadResponse>(world.LastValue);
        Assert.True(resp.Chars > 0);
    }
}
