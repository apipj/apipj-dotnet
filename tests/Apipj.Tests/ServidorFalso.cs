using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Apipj.Tests;

/// <summary>HttpMessageHandler falso: responde com funções enfileiradas (ou uma rota fixa) e grava as requisições.</summary>
internal sealed class ServidorFalso : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _fila = new();
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _padrao;

    public ServidorFalso(Func<HttpRequestMessage, HttpResponseMessage>? padrao = null) => _padrao = padrao;

    public ConcurrentQueue<HttpRequestMessage> Requisicoes { get; } = new();

    public int Total => Requisicoes.Count;

    public ServidorFalso Responder(Func<HttpRequestMessage, HttpResponseMessage> f)
    {
        _fila.Enqueue(f);
        return this;
    }

    public ServidorFalso Responder(HttpStatusCode status, string corpo, params (string Nome, string Valor)[] headers) =>
        Responder(_ => Resposta(status, corpo, headers));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requisicoes.Enqueue(request);
        if (_fila.TryDequeue(out var f)) return Task.FromResult(f(request));
        if (_padrao is not null) return Task.FromResult(_padrao(request));
        throw new InvalidOperationException("ServidorFalso sem resposta para " + request.RequestUri);
    }

    public static HttpResponseMessage Resposta(HttpStatusCode status, string corpo, params (string Nome, string Valor)[] headers)
    {
        var r = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        foreach (var (nome, valor) in headers) r.Headers.TryAddWithoutValidation(nome, valor);
        return r;
    }

    public static string Erro(string code, string message, string extras = "") =>
        $"{{\"status\":\"ERROR\",\"message\":\"{message}\",\"code\":\"{code}\"{extras}}}";

    public static string Dados(string arquivo) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Dados", arquivo));

    /// <summary>Documento mínimo válido para um CNPJ.</summary>
    public static string Doc(string cnpj, string razao = "EMPRESA TESTE LTDA") =>
        $"{{\"cnpj\":\"{cnpj}\",\"cnpj_formatado\":\"{Cnpj.Formatar(cnpj)}\",\"razao_social\":\"{razao}\",\"situacao_cadastral\":{{\"codigo\":\"02\",\"descricao\":\"ATIVA\",\"data\":null,\"motivo\":null}},\"atualizado_em\":\"2026-09\"}}";
}

/// <summary>Esperas registradas em vez de dormir.</summary>
internal sealed class EsperasFalsas
{
    public ConcurrentQueue<TimeSpan> Esperas { get; } = new();

    public Task Esperar(TimeSpan t, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Esperas.Enqueue(t);
        return Task.CompletedTask;
    }
}

internal static class Fabrica
{
    public const string ChaveTeste = "apipj_live_teste_0000000000000000";

    public static (ApipjClient Api, ServidorFalso Servidor, EsperasFalsas Esperas) Criar(ServidorFalso? servidor = null, string? chave = ChaveTeste)
    {
        servidor ??= new ServidorFalso();
        var esperas = new EsperasFalsas();
        var api = new ApipjClient(new HttpClient(servidor), chave, "https://teste.local")
        {
            Esperar = esperas.Esperar,
        };
        return (api, servidor, esperas);
    }
}
