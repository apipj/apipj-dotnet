namespace Apipj.Tests;

/// <summary>Só roda com APIPJ_TESTE_REAL=1 (e APIPJ_KEY para as consultas). Gasta poucas consultas da cota.</summary>
public sealed class FatoRealAttribute : FactAttribute
{
    public FatoRealAttribute(bool exigeChave = true)
    {
        if (Environment.GetEnvironmentVariable("APIPJ_TESTE_REAL") != "1")
            Skip = "Defina APIPJ_TESTE_REAL=1 para rodar contra a API real";
        else if (exigeChave && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APIPJ_KEY")))
            Skip = "Defina APIPJ_KEY para rodar as consultas reais";
    }
}

public class ApiRealTests
{
    private static ApipjClient Api() => new(baseUrl: Environment.GetEnvironmentVariable("APIPJ_BASE_URL"));

    [FatoReal]
    public async Task Consulta_Banco_do_Brasil()
    {
        using var api = Api();
        var doc = await api.ConsultarAsync("00.000.000/0001-91");

        Assert.NotNull(doc);
        Assert.Equal("BANCO DO BRASIL SA", doc!.RazaoSocial);
        Assert.Equal("5300108", doc.Endereco.Municipio.CodigoIbge);
        Assert.Equal("DF", doc.Endereco.Uf);
        Assert.NotNull(doc.DataInicioAtividade);
        Assert.NotEmpty(doc.Socios);
        Assert.NotNull(api.Limites?.PorMinutoLimite);
    }

    [FatoReal]
    public async Task Cnpj_valido_fora_da_base_devolve_null()
    {
        using var api = Api();
        Assert.Null(await api.ConsultarAsync("99999998999918"));
    }

    [FatoReal]
    public async Task Simplificado_e_uso()
    {
        using var api = Api();
        using var doc = await api.ConsultarSimplificadoAsync("191");
        Assert.Equal("BANCO DO BRASIL SA", doc!.RootElement.GetProperty("nome").GetString());

        var uso = await api.UsoAsync();
        Assert.False(string.IsNullOrEmpty(uso.Plan));
        Assert.True(uso.Quota > 0);
    }

    [FatoReal(exigeChave: false)]
    public async Task Status_e_validar_alfanumerico()
    {
        using var api = Api();
        var status = await api.StatusAsync();
        Assert.Equal("OK", status.Status);
        Assert.Matches(@"^\d{4}-\d{2}$", status.DatasetMonth);

        var v = await api.ValidarAsync("12.ABC.345/01DE-35");
        Assert.True(v.Valido);
        Assert.True(v.Alfanumerico);
        Assert.False((await api.ValidarAsync("12.ABC.345/01DE-36")).Valido);
    }
}
