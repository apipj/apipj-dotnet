using System.Net;
using System.Text.Json;
using static Apipj.Tests.ServidorFalso;

namespace Apipj.Tests;

public class ClienteTests
{
#if APIPJ_NETSTANDARD
    private static DateTime D(int a, int m, int d) => new(a, m, d);
#else
    private static DateOnly D(int a, int m, int d) => new(a, m, d);
#endif

    [Fact]
    public async Task Consultar_mapeia_amostra_real_do_Banco_do_Brasil()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, Dados("00000000000191.json"),
            ("X-RateLimit-Limit", "10"), ("X-RateLimit-Remaining", "9"), ("X-RateLimit-Reset", "60"),
            ("X-Quota-Limit", "50000"), ("X-Quota-Remaining", "49999"), ("X-Quota-Reset", "1793502000"));

        var doc = await api.ConsultarAsync("00.000.000/0001-91");

        Assert.NotNull(doc);
        Assert.Equal("00000000000191", doc!.Cnpj);
        Assert.Equal("00.000.000/0001-91", doc.CnpjFormatado);
        Assert.Equal("BANCO DO BRASIL SA", doc.RazaoSocial);
        Assert.Equal("DIRECAO GERAL", doc.NomeFantasia);
        Assert.Equal("MATRIZ", doc.MatrizFilial);
        Assert.Equal("ATIVA", doc.SituacaoCadastral.Descricao);
        Assert.Equal(D(2005, 11, 3), doc.SituacaoCadastral.Data);
        Assert.Equal("SEM MOTIVO", doc.SituacaoCadastral.Motivo?.Descricao);
        Assert.Null(doc.SituacaoEspecial?.Data);
        Assert.Equal(D(1966, 8, 1), doc.DataInicioAtividade);
        Assert.Equal("2038", doc.NaturezaJuridica?.Codigo);
        Assert.Equal("DEMAIS", doc.Porte?.Descricao);
        Assert.Equal(120000000000m, doc.CapitalSocial);
        Assert.Equal("6422100", doc.CnaePrincipal?.Codigo);
        Assert.Single(doc.CnaesSecundarios);
        Assert.Equal("5300108", doc.Endereco.Municipio.CodigoIbge);
        Assert.Equal("9701", doc.Endereco.Municipio.CodigoRfb);
        Assert.Equal("BRASILIA", doc.Endereco.Municipio.Nome);
        Assert.Equal("DF", doc.Endereco.Uf);
        Assert.Equal("70040912", doc.Endereco.Cep);
        Assert.Equal("61", doc.Telefones[0].Ddd);
        Assert.Equal("34931040", doc.Fax?.Numero);
        Assert.Equal("SECEX@BB.COM.BR", doc.Email);
        Assert.False(doc.Simples?.Optante);
        Assert.Equal(D(2007, 7, 1), doc.Simples?.DataOpcao);
        Assert.Equal(D(2009, 7, 1), doc.Mei?.DataExclusao);
        Assert.Equal(2, doc.Socios.Count);
        Assert.Equal("PESSOA FISICA", doc.Socios[0].Tipo);
        Assert.Equal("Diretor", doc.Socios[0].Qualificacao?.Descricao);
        Assert.Equal(D(2020, 7, 10), doc.Socios[0].DataEntrada);
        Assert.Null(doc.Socios[0].RepresentanteLegal);
        Assert.Equal("2026-09", doc.AtualizadoEm);
        Assert.Null(doc.Extras);

        var req = Assert.Single(srv.Requisicoes);
        Assert.Equal("https://teste.local/v1/cnpj/00000000000191", req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
        Assert.Equal(Fabrica.ChaveTeste, req.Headers.Authorization?.Parameter);
        Assert.StartsWith("apipj-dotnet/", req.Headers.UserAgent.ToString());

        Assert.Equal(10, api.Limites?.PorMinutoLimite);
        Assert.Equal(49999, api.Limites?.MensalRestante);
    }

    [Fact]
    public async Task Consultar_mapeia_representante_legal_datas_invalidas_e_campos_novos()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """
            {"cnpj":"11378117000120","cnpj_formatado":"11.378.117/0001-20","razao_social":"X","situacao_cadastral":{"codigo":"02","descricao":"ATIVA","data":"","motivo":null},
             "data_inicio_atividade":"0000-00-00","capital_social":1234.56,"campo_novo":{"a":1},
             "socios":[{"nome":"EMPRESA SOCIA","tipo":"ESTRANGEIRO","cpf_cnpj":null,"qualificacao":{"codigo":"37","descricao":"Sócio Pessoa Jurídica Domiciliado no Exterior"},
               "data_entrada":"2019-01-02","faixa_etaria":null,"pais":"PORTUGAL","representante_legal":{"cpf":"***111222**","nome":"FULANO","qualificacao":{"codigo":"17","descricao":"Procurador"}}}],
             "atualizado_em":"2026-09"}
            """);

        var doc = await api.ConsultarAsync("11378117000120");

        Assert.NotNull(doc);
        Assert.Null(doc!.SituacaoCadastral.Data);
        Assert.Null(doc.DataInicioAtividade);
        Assert.Equal(1234.56m, doc.CapitalSocial);
        Assert.Equal("PORTUGAL", doc.Socios[0].Pais);
        Assert.Equal("FULANO", doc.Socios[0].RepresentanteLegal?.Nome);
        Assert.Equal("Procurador", doc.Socios[0].RepresentanteLegal?.Qualificacao?.Descricao);
        Assert.True(doc.Extras!.ContainsKey("campo_novo"));
        Assert.Empty(doc.Telefones);
    }

    [Fact]
    public async Task Consultar_404_devolve_null()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.NotFound, Erro("not_found", "CNPJ não encontrado"));

        Assert.Null(await api.ConsultarAsync("99999998999918"));
        Assert.Equal(1, srv.Total);
    }

    [Fact]
    public async Task Consultar_404_sem_corpo_da_api_lanca()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.NotFound, "<html>not found</html>");

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("99999998999918"));
        Assert.Equal(404, e.StatusCode);
    }

    [Fact]
    public async Task Cnpj_invalido_nao_chama_a_api()
    {
        var (api, srv, _) = Fabrica.Criar();

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("12.ABC.345/01DE-36"));

        Assert.Equal(400, e.StatusCode);
        Assert.Equal("invalid_cnpj", e.Code);
        Assert.Equal("12ABC34501DE36", e.Cnpj);
        Assert.Equal(0, srv.Total);
        await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("00000000000000"));
        await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarSimplificadoAsync("abc"));
        Assert.Equal(0, srv.Total);
    }

    [Fact]
    public async Task Completa_zeros_a_esquerda_antes_de_consultar()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, Doc("00000000000191", "BANCO DO BRASIL SA"));

        var doc = await api.ConsultarAsync("191");

        Assert.Equal("BANCO DO BRASIL SA", doc?.RazaoSocial);
        Assert.EndsWith("/v1/cnpj/00000000000191", Assert.Single(srv.Requisicoes).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Rate_limited_espera_Retry_After_e_repete()
    {
        var (api, srv, esperas) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.TooManyRequests, Erro("rate_limited", "Limite de consultas por minuto excedido", ",\"retry_after\":7"), ("Retry-After", "7"))
           .Responder(HttpStatusCode.TooManyRequests, Erro("rate_limited", "Limite de consultas por minuto excedido"), ("Retry-After", "2"))
           .Responder(HttpStatusCode.OK, Doc("11378117000120"));

        var doc = await api.ConsultarAsync("11378117000120");

        Assert.NotNull(doc);
        Assert.Equal(3, srv.Total);
        Assert.Equal(new[] { TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(2) }, esperas.Esperas.ToArray());
    }

    [Fact]
    public async Task Rate_limited_usa_retry_after_do_corpo_sem_cabecalho()
    {
        var (api, srv, esperas) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.TooManyRequests, Erro("rate_limited", "Limite", ",\"retry_after\":5"))
           .Responder(HttpStatusCode.OK, Doc("11378117000120"));

        Assert.NotNull(await api.ConsultarAsync("11378117000120"));
        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(esperas.Esperas));
    }

    [Fact]
    public async Task Rate_limited_desiste_depois_de_3_retentativas()
    {
        var srv = new ServidorFalso(_ => Resposta(HttpStatusCode.TooManyRequests, Erro("rate_limited", "Limite"), ("Retry-After", "1")));
        var (api, _, esperas) = Fabrica.Criar(srv);

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal("rate_limited", e.Code);
        Assert.Equal(429, e.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), e.RetryAfter);
        Assert.Equal(4, srv.Total); // 1 + 3 retentativas
        Assert.Equal(3, esperas.Esperas.Count);
    }

    [Fact]
    public async Task Quota_exceeded_lanca_na_hora_com_links_de_assinatura()
    {
        var srv = new ServidorFalso(_ => Resposta(HttpStatusCode.TooManyRequests,
            Erro("quota_exceeded", "Cota mensal esgotada", ",\"upgrade_url\":\"https://apipj.com.br/precos\",\"reset\":1793502000")));
        var (api, _, esperas) = Fabrica.Criar(srv);

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal("quota_exceeded", e.Code);
        Assert.Equal(429, e.StatusCode);
        Assert.Equal(1, srv.Total);
        Assert.Empty(esperas.Esperas);
        Assert.Contains("https://apipj.com.br/painel?assinar=dev", e.Message);
        Assert.Contains("https://apipj.com.br/painel?assinar=pro", e.Message);
        Assert.StartsWith("Cota mensal esgotada", e.Message);
        Assert.Equal("https://apipj.com.br/precos", e.UpgradeUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793502000), e.Reset);
    }

    [Fact]
    public async Task Erro_5xx_repete_com_backoff_1_2_4()
    {
        var (api, srv, esperas) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.InternalServerError, "oops")
           .Responder(HttpStatusCode.BadGateway, "")
           .Responder(HttpStatusCode.ServiceUnavailable, Erro("dataset_unavailable", "Base indisponível"))
           .Responder(HttpStatusCode.OK, Doc("11378117000120"));

        Assert.NotNull(await api.ConsultarAsync("11378117000120"));
        Assert.Equal(4, srv.Total);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, esperas.Esperas.ToArray());
    }

    [Fact]
    public async Task Erro_5xx_esgotado_lanca_com_codigo()
    {
        var srv = new ServidorFalso(_ => Resposta(HttpStatusCode.ServiceUnavailable, Erro("dataset_unavailable", "Base indisponível")));
        var (api, _, _) = Fabrica.Criar(srv);
        api.MaxRetentativas = 1;

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal(503, e.StatusCode);
        Assert.Equal("dataset_unavailable", e.Code);
        Assert.Equal(2, srv.Total);
    }

    [Fact]
    public async Task Falha_de_rede_repete_e_depois_lanca_network_error()
    {
        var srv = new ServidorFalso(_ => throw new HttpRequestException("conexão recusada"));
        var (api, _, esperas) = Fabrica.Criar(srv);

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal(0, e.StatusCode);
        Assert.Equal("network_error", e.Code);
        Assert.Equal(4, srv.Total);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, esperas.Esperas.ToArray());
    }

    [Fact]
    public async Task Tempo_esgotado_vira_timeout()
    {
        var srv = new ServidorFalsoLento();
        var api = new ApipjClient(new HttpClient(srv), Fabrica.ChaveTeste, "https://teste.local")
        {
            TimeoutPorTentativa = TimeSpan.FromMilliseconds(50),
            MaxRetentativas = 0,
        };

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));
        Assert.Equal("timeout", e.Code);
    }

    [Fact]
    public async Task Cancelamento_do_chamador_nao_vira_timeout()
    {
        var api = new ApipjClient(new HttpClient(new ServidorFalsoLento()), Fabrica.ChaveTeste, "https://teste.local");
        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.ConsultarAsync("11378117000120", cts.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData(HttpStatusCode.BadRequest, "invalid_cnpj")]
    public async Task Erros_4xx_lancam_sem_repetir(HttpStatusCode status, string code)
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(status, Erro(code, "erro"));

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal((int)status, e.StatusCode);
        Assert.Equal(code, e.Code);
        Assert.Equal(1, srv.Total);
    }

    [Fact]
    public async Task Sem_chave_lanca_unauthorized_sem_chamar_a_api()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APIPJ_KEY"))) return; // ambiente com chave real
        var (api, srv, _) = Fabrica.Criar(chave: null);

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.ConsultarAsync("11378117000120"));

        Assert.Equal("unauthorized", e.Code);
        Assert.Contains("APIPJ_KEY", e.Message);
        Assert.Contains("https://apipj.com.br/criar-conta", e.Message);
        Assert.Equal(0, srv.Total);
    }

    [Fact]
    public async Task Simplificado_devolve_JsonDocument()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, Dados("00000000000191-simplificado.json"));

        using var doc = await api.ConsultarSimplificadoAsync("00000000000191");

        Assert.NotNull(doc);
        Assert.Equal("BANCO DO BRASIL SA", doc!.RootElement.GetProperty("nome").GetString());
        Assert.Equal("01/08/1966", doc.RootElement.GetProperty("abertura").GetString());
        Assert.Equal("https://teste.local/v1/cnpj/00000000000191?formato=simplificado", Assert.Single(srv.Requisicoes).RequestUri!.ToString());
    }

    [Fact]
    public async Task Validar_status_e_uso()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, """{"cnpj":"12ABC34501DE35","valido":true,"formatado":"12.ABC.345/01DE-35","alfanumerico":true,"erro":null}""")
           .Responder(HttpStatusCode.OK, """{"status":"OK","dataset_month":"2026-09","dataset_generated_at":"2026-10-09T20:31:43.201Z","rows":73366147,"version":"0.1.0","time":"2026-10-14T01:46:45.950Z"}""")
           .Responder(HttpStatusCode.OK, """{"plan":"free","plan_name":"Gratuito","plan_price":0,"plan_valid_until":null,"limits":{"rpm":10,"monthly_quota":50000},"usage":{"month":"2026-10"},"used":12,"quota":50000,"remaining":49988,"reset":1793502000,"reset_at":"2026-11-01T03:00:00.000Z","period":"2026-10","auth":"key"}""");

        var v = await api.ValidarAsync("12.abc.345/01de-35");
        Assert.True(v.Valido);
        Assert.True(v.Alfanumerico);
        Assert.Equal("12.ABC.345/01DE-35", v.Formatado);

        var s = await api.StatusAsync();
        Assert.Equal("OK", s.Status);
        Assert.Equal("2026-09", s.DatasetMonth);
        Assert.Equal(73366147, s.Rows);
        Assert.Equal(2026, s.DatasetGeneratedAt?.Year);

        var u = await api.UsoAsync();
        Assert.Equal("free", u.Plan);
        Assert.Equal(10, u.Limits?.Rpm);
        Assert.Equal(50000, u.Limits?.MonthlyQuota);
        Assert.Equal(49988, u.Remaining);
        Assert.Equal("2026-10", u.Period);

        var reqs = srv.Requisicoes.ToArray();
        Assert.Equal("/v1/valida/12ABC34501DE35", reqs[0].RequestUri!.AbsolutePath);
        Assert.Null(reqs[0].Headers.Authorization); // validar e status não mandam a chave
        Assert.Null(reqs[1].Headers.Authorization);
        Assert.Equal("/v1/usage", reqs[2].RequestUri!.AbsolutePath);
        Assert.NotNull(reqs[2].Headers.Authorization);
    }

    [Fact]
    public async Task Validar_vazio_responde_local()
    {
        var (api, srv, _) = Fabrica.Criar();
        var v = await api.ValidarAsync("");
        Assert.False(v.Valido);
        Assert.Equal("CNPJ vazio", v.Erro);
        Assert.Equal(0, srv.Total);
    }

    [Fact]
    public async Task Resposta_que_nao_e_json_vira_invalid_response()
    {
        var (api, srv, _) = Fabrica.Criar();
        srv.Responder(HttpStatusCode.OK, "<html>proxy</html>");

        var e = await Assert.ThrowsAsync<ApipjException>(() => api.StatusAsync());
        Assert.Equal("invalid_response", e.Code);
    }

    [Theory]
    [InlineData(null, "https://apipj.com.br")]
    [InlineData("https://apipj.com.br/v1", "https://apipj.com.br")]
    [InlineData("https://apipj.com.br/v1/", "https://apipj.com.br")]
    [InlineData("http://localhost:3000/", "http://localhost:3000")]
    public void BaseUrl_normalizada(string? baseUrl, string esperado)
    {
        using var api = new ApipjClient(baseUrl: baseUrl);
        Assert.Equal(esperado, api.BaseUrl);
    }

    [Fact]
    public void Documento_serializa_de_volta_em_snake_case()
    {
        var doc = JsonSerializer.Deserialize<DocumentoCompleto>(Dados("00000000000191.json"));
        var json = JsonSerializer.Serialize(doc);
        Assert.Contains("\"razao_social\":\"BANCO DO BRASIL SA\"", json);
        Assert.Contains("\"codigo_ibge\":\"5300108\"", json);
    }

    private sealed class ServidorFalsoLento : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
