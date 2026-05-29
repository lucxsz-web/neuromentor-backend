using NeuroMentor.Api.Services;
using Reqnroll;
using Xunit;

namespace NeuroMentor.Api.Tests.Steps;

[Binding]
public class ExtracaoTextoSteps
{
    private string _texto = "";
    private string _resultado = "";

    [Given(@"o texto bruto:")]
    public void DadoTextoBruto(string texto) => _texto = texto;

    [When(@"aplico a limpeza de texto")]
    public void QuandoLimpo() => _resultado = TextExtractionService.Clean(_texto);

    [When(@"extraio o trecho para o módulo ""(.*)"" com os conceitos ""(.*)""")]
    public void QuandoExtraioTrecho(string titulo, string conceitos)
    {
        var lista = conceitos.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _resultado = TextExtractionService.ExtractChunk(_texto, titulo, lista);
    }

    [Then(@"o texto limpo deve conter ""(.*)""")]
    public void EntaoLimpoContem(string esperado) => Assert.Contains(esperado, _resultado);

    [Then(@"o texto limpo não deve conter ""(.*)""")]
    public void EntaoLimpoNaoContem(string indesejado) => Assert.DoesNotContain(indesejado, _resultado);

    [Then(@"o trecho extraído deve conter ""(.*)""")]
    public void EntaoTrechoContem(string esperado) =>
        Assert.Contains(esperado, _resultado, StringComparison.OrdinalIgnoreCase);
}
