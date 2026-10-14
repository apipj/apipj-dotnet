using System.Net;
using static Apipj.Tests.ServidorFalso;

namespace Apipj.Tests;

public class VariosTests
{
    private static string CnpjDaUrl(HttpRequestMessage r) => r.RequestUri!.AbsolutePath.Split('/').Last();

    private static async Task<List<ResultadoConsulta>> Coletar(IAsyncEnumerable<ResultadoConsulta> fonte)
    {
        var lista = new List<ResultadoConsulta>();
        await foreach (var r in fonte) lista.Add(r);
        return lista;
    }

    [Fact]
    public async Task Deduplica_pula_invalidos_e_entrega_404_como_null()
    {
        var srv = new ServidorFalso(r => CnpjDaUrl(r) switch
        {
            "99999998999918" => Resposta(HttpStatusCode.NotFound, Erro("not_found", "CNPJ não encontrado")),
            var c => Resposta(HttpStatusCode.OK, Doc(c, "EMPRESA " + c), ("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "999")),
        });
        var (api, _, _) = Fabrica.Criar(srv);
        var lista = new[] { "11.378.117/0001-20", "00000000000191", "11378117000120", "123", "99999998999918", "191", "12.ABC.345/01DE-35" };

        var res = await Coletar(api.ConsultarVariosAsync(lista, simultaneas: 3));

        Assert.Equal(5, res.Count); // 11378117000120, 00000000000191, 00000000000123 (inválido), 99999998999918, 12ABC34501DE35
        Assert.Equal(4, srv.Total); // o inválido não chama a API
        var porCnpj = res.ToDictionary(r => r.Cnpj);
        Assert.Equal("EMPRESA 11378117000120", porCnpj["11378117000120"].Documento?.RazaoSocial);
        Assert.Equal("EMPRESA 00000000000191", porCnpj["00000000000191"].Documento?.RazaoSocial);
        Assert.Null(porCnpj["99999998999918"].Documento);
        Assert.Null(porCnpj["99999998999918"].Erro);
        Assert.Equal("invalid_cnpj", porCnpj["00000000000123"].Erro?.Code);
        Assert.Equal("EMPRESA 12ABC34501DE35", porCnpj["12ABC34501DE35"].Documento?.RazaoSocial);
    }

    [Fact]
    public async Task Erro_de_um_cnpj_sai_no_item_e_o_lote_continua()
    {
        var srv = new ServidorFalso(r => CnpjDaUrl(r) == "00000000000191"
            ? Resposta(HttpStatusCode.InternalServerError, "falhou")
            : Resposta(HttpStatusCode.OK, Doc(CnpjDaUrl(r))));
        var (api, _, _) = Fabrica.Criar(srv);
        api.MaxRetentativas = 1;

        var res = await Coletar(api.ConsultarVariosAsync(new[] { "00000000000191", "11378117000120" }, simultaneas: 1));

        Assert.Equal(2, res.Count);
        Assert.Equal("server_error", res.Single(r => r.Cnpj == "00000000000191").Erro?.Code);
        Assert.NotNull(res.Single(r => r.Cnpj == "11378117000120").Documento);
    }

    [Fact]
    public async Task Quota_exceeded_interrompe_o_lote_lancando()
    {
        var srv = new ServidorFalso(r => Resposta(HttpStatusCode.TooManyRequests, Erro("quota_exceeded", "Cota mensal esgotada")));
        var (api, _, _) = Fabrica.Criar(srv);
        var lista = Enumerable.Range(1, 50).Select(i => $"{i:D12}").Select(b => b + Cnpj.CalcularDv(b)).ToArray();

        var e = await Assert.ThrowsAsync<ApipjException>(() => Coletar(api.ConsultarVariosAsync(lista, simultaneas: 4)));

        Assert.Equal("quota_exceeded", e.Code);
        Assert.Equal(1, srv.Total); // a sonda falhou de vez: ninguém mais chama a API
    }

    [Fact]
    public async Task Respeita_o_limite_por_minuto_lido_dos_cabecalhos()
    {
        var srv = new ServidorFalso(r => Resposta(HttpStatusCode.OK, Doc(CnpjDaUrl(r)), ("X-RateLimit-Limit", "2"), ("X-RateLimit-Remaining", "1")));
        var api = new ApipjClient(new HttpClient(srv), Fabrica.ChaveTeste, "https://teste.local")
        {
            JanelaLimitador = TimeSpan.FromMilliseconds(300),
        };
        var lista = Enumerable.Range(1, 5).Select(i => $"{i:D12}").Select(b => b + Cnpj.CalcularDv(b)).ToArray();

        var inicio = DateTime.UtcNow;
        var res = await Coletar(api.ConsultarVariosAsync(lista, simultaneas: 5));
        var duracao = DateTime.UtcNow - inicio;

        Assert.Equal(5, res.Count);
        Assert.All(res, r => Assert.NotNull(r.Documento));
        // 2 por janela de 300 ms: 5 consultas precisam de pelo menos 2 janelas cheias
        Assert.True(duracao >= TimeSpan.FromMilliseconds(550), $"durou {duracao.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task Break_no_laco_cancela_o_resto()
    {
        var srv = new ServidorFalso(r => Resposta(HttpStatusCode.OK, Doc(CnpjDaUrl(r))));
        var (api, _, _) = Fabrica.Criar(srv);
        var lista = Enumerable.Range(1, 200).Select(i => $"{i:D12}").Select(b => b + Cnpj.CalcularDv(b)).ToArray();

        var n = 0;
        await foreach (var _ in api.ConsultarVariosAsync(lista, simultaneas: 2))
        {
            if (++n == 3) break;
        }

        Assert.Equal(3, n);
        Assert.True(srv.Total < 20, $"fez {srv.Total} requisições");
    }

    [Fact]
    public void Argumentos_invalidos_lancam_na_chamada()
    {
        var (api, _, _) = Fabrica.Criar();
        Assert.Throws<ArgumentNullException>(() => api.ConsultarVariosAsync(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => api.ConsultarVariosAsync(new[] { "1" }, simultaneas: 0));
    }
}
