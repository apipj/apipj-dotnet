using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Apipj
{
    /// <summary>
    /// Cliente da API de CNPJ do apipj (https://apipj.com.br).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chave de API é secreta: use este cliente só no servidor (back-end, serviços, funções, scripts).
    /// Nunca distribua a chave em apps desktop/mobile ou em código que roda no navegador (Blazor WebAssembly).
    /// </para>
    /// <para>
    /// Thread-safe: crie uma instância e reaproveite. No ASP.NET Core, registre com <c>AddHttpClient</c>
    /// (veja o README). Repete sozinho em <c>429 rate_limited</c> (espera <c>Retry-After</c>), <c>5xx</c>,
    /// falha de rede e tempo esgotado (espera 1 s, 2 s, 4 s), até <see cref="MaxRetentativas"/> vezes.
    /// </para>
    /// </remarks>
    public sealed class ApipjClient : IDisposable
    {
        /// <summary>URL base padrão da API.</summary>
        public const string BaseUrlPadrao = "https://apipj.com.br";

        /// <summary>Versão deste pacote (vai no <c>User-Agent</c>).</summary>
        public const string Versao = "0.1.0";

        /// <summary>Nome da variável de ambiente lida quando a chave não é passada no construtor.</summary>
        public const string VariavelChave = "APIPJ_KEY";

        private const string UrlCriarConta = "https://apipj.com.br/criar-conta";
        private const string UrlAssinarDev = "https://apipj.com.br/painel?assinar=dev";
        private const string UrlAssinarPro = "https://apipj.com.br/painel?assinar=pro";
        private static readonly string UserAgent = "apipj-dotnet/" + Versao;

        private readonly HttpClient _http;
        private readonly bool _httpProprio;
        private readonly string? _chave;
        private Limites? _limites;

        /// <summary>
        /// Cria o cliente.
        /// </summary>
        /// <param name="http">
        /// <see cref="HttpClient"/> a usar (por exemplo, o entregue pelo <c>IHttpClientFactory</c>). Se <c>null</c>,
        /// o cliente cria e descarta o seu. O cliente não altera <c>BaseAddress</c> nem os cabeçalhos padrão do <see cref="HttpClient"/> recebido.
        /// </param>
        /// <param name="apiKey">Chave de API (<c>apipj_live_...</c>). Se <c>null</c>, lê a variável de ambiente <c>APIPJ_KEY</c> na hora de consultar.</param>
        /// <param name="baseUrl">URL base da API (padrão <c>https://apipj.com.br</c>; um <c>/v1</c> no fim é aceito e ignorado).</param>
        public ApipjClient(HttpClient? http = null, string? apiKey = null, string? baseUrl = null)
        {
            _httpProprio = http is null;
            _http = http ?? new HttpClient();
            _chave = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey!.Trim();
            var b = string.IsNullOrWhiteSpace(baseUrl) ? BaseUrlPadrao : baseUrl!.Trim().TrimEnd('/');
            if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) b = b.Substring(0, b.Length - 3);
            BaseUrl = b;
        }

        /// <summary>URL base da API, sem barra e sem <c>/v1</c> no fim.</summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Quantas vezes repetir uma chamada depois da primeira tentativa, em <c>429 rate_limited</c>, <c>5xx</c>,
        /// falha de rede ou tempo esgotado (padrão 3: esperas de 1 s, 2 s e 4 s; 0 desliga).
        /// </summary>
        public int MaxRetentativas { get; set; } = 3;

        /// <summary>Tempo máximo de cada tentativa (padrão 10 s). <see cref="Timeout.InfiniteTimeSpan"/> desliga.</summary>
        public TimeSpan TimeoutPorTentativa { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>Limites informados pela última consulta autenticada (cabeçalhos <c>X-RateLimit-*</c> e <c>X-Quota-*</c>), ou <c>null</c>.</summary>
        public Limites? Limites => Volatile.Read(ref _limites);

        /// <summary>Espera entre tentativas (trocada nos testes para não dormir de verdade).</summary>
        internal Func<TimeSpan, CancellationToken, Task> Esperar { get; set; } = (t, ct) => Task.Delay(t, ct);

        /// <summary>Janela do limitador do lote (60 s; trocada nos testes).</summary>
        internal TimeSpan JanelaLimitador { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Consulta os dados cadastrais de um CNPJ no formato completo (exige chave).
        /// Valida formato e dígito verificador localmente antes de chamar a API: CNPJ inválido não gasta cota.
        /// </summary>
        /// <param name="cnpj">CNPJ numérico ou alfanumérico, com ou sem máscara.</param>
        /// <param name="ct">Cancelamento.</param>
        /// <returns>O documento, ou <c>null</c> se o CNPJ é válido mas não está na base da Receita (404).</returns>
        /// <exception cref="ApipjException">
        /// 400 <c>invalid_cnpj</c> (sem chamar a API), 401 <c>unauthorized</c>, 429 <c>quota_exceeded</c>,
        /// 429 <c>rate_limited</c> depois das tentativas, 5xx, <c>network_error</c> ou <c>timeout</c> depois das tentativas.
        /// </exception>
        public async Task<DocumentoCompleto?> ConsultarAsync(string cnpj, CancellationToken ct = default)
        {
            var v = ValidarOuLancar(cnpj);
            var r = await ConsultarBrutoAsync(v.Cnpj, simplificado: false, limitador: null, ct).ConfigureAwait(false);
            return r is null ? null : Desserializar(r, ApipjJsonContext.Default.DocumentoCompleto);
        }

        /// <summary>
        /// Consulta um CNPJ no formato <c>simplificado</c> (plano, com datas DD/MM/AAAA e CNPJ, CEP e telefone já formatados).
        /// Devolve o JSON como <see cref="JsonDocument"/>: descarte-o (<c>using</c>) depois de ler.
        /// </summary>
        /// <param name="cnpj">CNPJ numérico ou alfanumérico, com ou sem máscara.</param>
        /// <param name="ct">Cancelamento.</param>
        /// <returns>O documento, ou <c>null</c> se o CNPJ não está na base (404).</returns>
        /// <exception cref="ApipjException">Os mesmos casos de <see cref="ConsultarAsync"/>.</exception>
        public async Task<JsonDocument?> ConsultarSimplificadoAsync(string cnpj, CancellationToken ct = default)
        {
            var v = ValidarOuLancar(cnpj);
            var r = await ConsultarBrutoAsync(v.Cnpj, simplificado: true, limitador: null, ct).ConfigureAwait(false);
            if (r is null) return null;
            try
            {
                return JsonDocument.Parse(r.Corpo);
            }
            catch (JsonException e)
            {
                throw RespostaInvalida(r, e);
            }
        }

        /// <summary>
        /// Consulta vários CNPJs em paralelo, respeitando o limite por minuto do plano (lido de <c>X-RateLimit-*</c>).
        /// Entrega um <see cref="ResultadoConsulta"/> por CNPJ, na ordem de conclusão.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>CNPJs repetidos (depois de normalizados) são consultados uma vez só.</item>
        /// <item>CNPJs inválidos não chamam a API: saem com <c>Documento = null</c> e <c>Erro.Code == "invalid_cnpj"</c>.</item>
        /// <item>Erro de um CNPJ (5xx ou rede depois das tentativas) sai no próprio item e o lote continua.</item>
        /// <item>401 e cota mensal esgotada (<c>quota_exceeded</c>) interrompem o lote lançando <see cref="ApipjException"/>,
        /// depois de entregar os resultados já prontos.</item>
        /// <item>A primeira consulta sai sozinha, para descobrir o limite do plano; um <c>break</c> no <c>await foreach</c> cancela as demais.</item>
        /// </list>
        /// </remarks>
        /// <param name="cnpjs">Lista de CNPJs, com ou sem máscara.</param>
        /// <param name="simultaneas">Consultas em paralelo (padrão 4).</param>
        /// <param name="ct">Cancelamento.</param>
        public IAsyncEnumerable<ResultadoConsulta> ConsultarVariosAsync(IEnumerable<string> cnpjs, int simultaneas = 4, CancellationToken ct = default)
        {
            if (cnpjs is null) throw new ArgumentNullException(nameof(cnpjs));
            if (simultaneas < 1) throw new ArgumentOutOfRangeException(nameof(simultaneas), "simultaneas deve ser 1 ou mais");
            return ConsultarVariosInternoAsync(cnpjs, simultaneas, ct);
        }

        /// <summary>
        /// Valida formato e dígito verificador pela API (<c>GET /v1/valida/{cnpj}</c>: sem chave e sem consumo de cota).
        /// Para validar offline, use <see cref="Cnpj.Valido"/> ou <see cref="Cnpj.ValidarLocal"/>.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        /// <param name="ct">Cancelamento.</param>
        public async Task<Validacao> ValidarAsync(string cnpj, CancellationToken ct = default)
        {
            var c = Cnpj.Normalizar(cnpj);
            if (c.Length == 0 || c.Length > 32) return Cnpj.ValidarLocal(c);
            var r = await EnviarAsync("/v1/valida/" + Uri.EscapeDataString(c), autenticar: false, limitador: null, ct).ConfigureAwait(false);
            return ExigirJson(r, ApipjJsonContext.Default.Validacao);
        }

        /// <summary>Status do serviço e mês do lote da Receita carregado (<c>GET /v1/status</c>, sem chave).</summary>
        /// <param name="ct">Cancelamento.</param>
        public async Task<StatusBase> StatusAsync(CancellationToken ct = default)
        {
            var r = await EnviarAsync("/v1/status", autenticar: false, limitador: null, ct).ConfigureAwait(false);
            return ExigirJson(r, ApipjJsonContext.Default.StatusBase);
        }

        /// <summary>Plano, limites e uso do mês da chave (<c>GET /v1/usage</c>: exige chave, não consome cota).</summary>
        /// <param name="ct">Cancelamento.</param>
        public async Task<Uso> UsoAsync(CancellationToken ct = default)
        {
            var r = await EnviarAsync("/v1/usage", autenticar: true, limitador: null, ct).ConfigureAwait(false);
            return ExigirJson(r, ApipjJsonContext.Default.Uso);
        }

        /// <summary>Descarta o <see cref="HttpClient"/> interno, se o cliente o criou.</summary>
        public void Dispose()
        {
            if (_httpProprio) _http.Dispose();
        }

        // ------------------------------------------------------------------ internos

        private async IAsyncEnumerable<ResultadoConsulta> ConsultarVariosInternoAsync(
            IEnumerable<string> cnpjs, int simultaneas, [EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = cts.Token;
            var limitador = new Limitador(JanelaLimitador);
            var fonte = cnpjs.GetEnumerator();
            var fonteTrava = new object();
            var fonteAcabou = false;
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var prontos = new ConcurrentQueue<ResultadoConsulta>();
            var sinal = new SemaphoreSlim(0);
            // contrapressão: não acumula resultados além do que o consumidor está lendo
            var espaco = new SemaphoreSlim(simultaneas * 2);
            Exception? fatal = null;

            void Entregar(ResultadoConsulta item)
            {
                prontos.Enqueue(item);
                sinal.Release();
            }

            void Interromper(Exception e)
            {
                Interlocked.CompareExchange(ref fatal, e, null);
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
            }

            async Task TrabalharAsync()
            {
                try
                {
                    while (Volatile.Read(ref fatal) is null && !token.IsCancellationRequested)
                    {
                        await espaco.WaitAsync(token).ConfigureAwait(false);
                        string? entrada = null;
                        var tem = false;
                        lock (fonteTrava)
                        {
                            if (!fonteAcabou)
                            {
                                tem = fonte.MoveNext();
                                if (tem) entrada = fonte.Current;
                                else fonteAcabou = true;
                            }
                        }
                        if (!tem)
                        {
                            espaco.Release();
                            return;
                        }

                        var v = Cnpj.ValidarLocal(entrada);
                        bool novo;
                        lock (vistos) novo = vistos.Add(v.Cnpj);
                        if (!novo)
                        {
                            espaco.Release();
                            continue;
                        }
                        if (!v.Valido)
                        {
                            Entregar(new ResultadoConsulta { Cnpj = v.Cnpj, Erro = CnpjInvalido(v) });
                            continue;
                        }

                        var sonda = false;
                        try
                        {
                            sonda = await limitador.EntrarAsync(token).ConfigureAwait(false);
                            var r = await ConsultarBrutoAsync(v.Cnpj, simplificado: false, limitador, token).ConfigureAwait(false);
                            var doc = r is null ? null : Desserializar(r, ApipjJsonContext.Default.DocumentoCompleto);
                            Entregar(new ResultadoConsulta { Cnpj = v.Cnpj, Documento = doc });
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            espaco.Release();
                            return;
                        }
                        catch (ApipjException e) when (e.Code == "unauthorized" || e.Code == "quota_exceeded")
                        {
                            // valeria para todos os CNPJs seguintes: para o lote
                            espaco.Release();
                            Interromper(e);
                            return;
                        }
                        catch (ApipjException e)
                        {
                            Entregar(new ResultadoConsulta { Cnpj = v.Cnpj, Erro = e });
                        }
                        finally
                        {
                            if (sonda) limitador.LiberarSonda();
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception e)
                {
                    // erro vindo do enumerável de entrada (ou inesperado)
                    Interromper(e);
                }
                finally
                {
                    sinal.Release();
                }
            }

            var tarefas = Enumerable.Range(0, simultaneas).Select(_ => Task.Run(TrabalharAsync)).ToArray();
            var encerrados = 0;
            try
            {
                // cada item entregue e cada trabalhador encerrado liberam o sinal uma vez
                while (encerrados < simultaneas)
                {
                    await sinal.WaitAsync(ct).ConfigureAwait(false);
                    if (prontos.TryDequeue(out var item))
                    {
                        espaco.Release();
                        yield return item;
                    }
                    else
                    {
                        encerrados++;
                    }
                }
                var erro = Volatile.Read(ref fatal);
                if (erro is not null) ExceptionDispatchInfo.Capture(erro).Throw();
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                limitador.LiberarSonda();
                try { await Task.WhenAll(tarefas).ConfigureAwait(false); } catch { /* já tratado nos trabalhadores */ }
                fonte.Dispose();
                sinal.Dispose();
                espaco.Dispose();
            }
        }

        private static Validacao ValidarOuLancar(string cnpj)
        {
            var v = Cnpj.ValidarLocal(cnpj);
            if (!v.Valido) throw CnpjInvalido(v);
            return v;
        }

        private static ApipjException CnpjInvalido(Validacao v) =>
            new ApipjException(400, "invalid_cnpj", v.Erro ?? "CNPJ inválido") { Cnpj = v.Cnpj };

        private async Task<Resposta?> ConsultarBrutoAsync(string cnpj, bool simplificado, Limitador? limitador, CancellationToken ct)
        {
            var caminho = "/v1/cnpj/" + Uri.EscapeDataString(cnpj) + (simplificado ? "?formato=simplificado" : string.Empty);
            var r = await EnviarAsync(caminho, autenticar: true, limitador, ct).ConfigureAwait(false);
            if (r.Status == 404)
            {
                var erro = ErroDaResposta(r);
                if (erro.Code == "not_found" && r.TemCorpoJson) return null;
                throw erro;
            }
            return r;
        }

        private string ObterChave()
        {
            var chave = _chave ?? Environment.GetEnvironmentVariable(VariavelChave);
            if (string.IsNullOrWhiteSpace(chave))
            {
                throw new ApipjException(401, "unauthorized",
                    "Chave de API ausente: passe apiKey no construtor de ApipjClient ou defina a variável de ambiente APIPJ_KEY. Conta grátis em " + UrlCriarConta);
            }
            return chave!.Trim();
        }

        /// <summary>
        /// Faz a chamada com repetição: 429 <c>rate_limited</c> espera Retry-After; 5xx, rede e tempo esgotado esperam 1 s, 2 s, 4 s.
        /// Devolve a resposta (2xx ou 404) com o corpo já lido; os demais status viram <see cref="ApipjException"/>.
        /// </summary>
        private async Task<Resposta> EnviarAsync(string caminho, bool autenticar, Limitador? limitador, CancellationToken ct)
        {
            var chave = autenticar ? ObterChave() : null;
            var url = BaseUrl + caminho;
            var maxRetentativas = Math.Max(0, MaxRetentativas);

            for (var tentativa = 0; ; tentativa++)
            {
                var ultima = tentativa >= maxRetentativas;
                if (limitador is not null) await limitador.AguardarJanelaAsync(Esperar, ct).ConfigureAwait(false);

                Resposta r;
                try
                {
                    r = await TentarAsync(url, chave, ct).ConfigureAwait(false);
                }
                catch (ApipjException e) when (e.StatusCode == 0)
                {
                    if (ultima) throw;
                    await Esperar(Backoff(tentativa), ct).ConfigureAwait(false);
                    continue;
                }

                if (autenticar) GuardarLimites(r);
                limitador?.Registrar(r.RateLimitLimit, r.RateLimitRemaining);

                if ((r.Status >= 200 && r.Status < 300) || r.Status == 404) return r;

                var erro = ErroDaResposta(r);
                if (r.Status == 429 && erro.Code != "quota_exceeded")
                {
                    if (ultima) throw erro;
                    var espera = erro.RetryAfter ?? TimeSpan.FromSeconds(1);
                    limitador?.Bloquear(espera);
                    await Esperar(espera, ct).ConfigureAwait(false);
                    continue;
                }
                if (r.Status >= 500)
                {
                    if (ultima) throw erro;
                    await Esperar(Backoff(tentativa), ct).ConfigureAwait(false);
                    continue;
                }
                throw erro;
            }
        }

        private async Task<Resposta> TentarAsync(string url, string? chave, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            if (chave is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var timeout = TimeoutPorTentativa;
            if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan) cts.CancelAfter(timeout);
            try
            {
                using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                var corpo = await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
#else
                var corpo = res.Content is null ? string.Empty : await res.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                return new Resposta((int)res.StatusCode, corpo ?? string.Empty, res.Headers);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException e)
            {
                throw new ApipjException(0, "timeout",
                    $"Sem resposta da API em {timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s", e);
            }
            catch (HttpRequestException e)
            {
                throw new ApipjException(0, "network_error", "Falha de rede ao chamar a API: " + e.Message, e);
            }
        }

        private static TimeSpan Backoff(int tentativa) =>
            TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, tentativa)));

        private void GuardarLimites(Resposta r)
        {
            if (r.RateLimitLimit is null && r.QuotaLimit is null) return;
            Volatile.Write(ref _limites, new Limites
            {
                PorMinutoLimite = r.RateLimitLimit,
                PorMinutoRestante = r.RateLimitRemaining,
                PorMinutoReset = r.RateLimitReset,
                MensalLimite = r.QuotaLimit,
                MensalRestante = r.QuotaRemaining,
                MensalReset = r.QuotaReset,
            });
        }

        private static T Desserializar<T>(Resposta r, JsonTypeInfo<T> tipo) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize(r.Corpo, tipo) ?? throw RespostaInvalida(r, null);
            }
            catch (JsonException e)
            {
                throw RespostaInvalida(r, e);
            }
        }

        private static T ExigirJson<T>(Resposta r, JsonTypeInfo<T> tipo) where T : class
        {
            if (r.Status < 200 || r.Status >= 300) throw ErroDaResposta(r);
            return Desserializar(r, tipo);
        }

        private static ApipjException RespostaInvalida(Resposta r, Exception? causa) =>
            new ApipjException(r.Status, "invalid_response", "Resposta inesperada da API (não é o JSON esperado)", causa) { Corpo = r.Corpo };

        private static ApipjException ErroDaResposta(Resposta r)
        {
            CorpoErro? c = null;
            if (r.TemCorpoJson)
            {
                try { c = JsonSerializer.Deserialize(r.Corpo, ApipjJsonContext.Default.CorpoErro); }
                catch (JsonException) { }
            }
            var code = string.IsNullOrEmpty(c?.Code) ? CodigoPadrao(r.Status) : c!.Code!;
            var mensagem = string.IsNullOrEmpty(c?.Message) ? $"A API respondeu HTTP {r.Status}" : c!.Message!;
            if (code == "quota_exceeded" && mensagem.IndexOf("assinar=", StringComparison.Ordinal) < 0)
            {
                mensagem = mensagem.TrimEnd('.') +
                    $". Para continuar agora, assine o plano Dev ({UrlAssinarDev}) ou Pro ({UrlAssinarPro}).";
            }

            TimeSpan? retryAfter = null;
            if (r.Status == 429)
            {
                retryAfter = r.RetryAfter;
                if (retryAfter is null && c?.RetryAfter is double s && s >= 0) retryAfter = TimeSpan.FromSeconds(s);
            }

            return new ApipjException(r.Status, code, mensagem)
            {
                RetryAfter = retryAfter,
                UpgradeUrl = c?.UpgradeUrl,
                Reset = c?.Reset is long reset && reset > 0 ? DateTimeOffset.FromUnixTimeSeconds(reset) : null,
                Cnpj = c?.Cnpj,
                Corpo = r.Corpo,
            };
        }

        private static string CodigoPadrao(int status)
        {
            switch (status)
            {
                case 400: return "bad_request";
                case 401: return "unauthorized";
                case 403: return "forbidden";
                case 404: return "not_found";
                case 429: return "rate_limited";
                case 503: return "dataset_unavailable";
                default: return status >= 500 ? "server_error" : "http_error";
            }
        }

        /// <summary>Resposta já lida (status, corpo e cabeçalhos de limite).</summary>
        private sealed class Resposta
        {
            public Resposta(int status, string corpo, HttpResponseHeaders headers)
            {
                Status = status;
                Corpo = corpo;
                RateLimitLimit = Numero(headers, "X-RateLimit-Limit");
                RateLimitRemaining = Numero(headers, "X-RateLimit-Remaining");
                RateLimitReset = Numero(headers, "X-RateLimit-Reset");
                QuotaLimit = Numero(headers, "X-Quota-Limit");
                QuotaRemaining = Numero(headers, "X-Quota-Remaining");
                QuotaReset = Numero(headers, "X-Quota-Reset");
                var ra = headers.RetryAfter;
                if (ra?.Delta is TimeSpan delta) RetryAfter = delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
                else if (ra?.Date is DateTimeOffset data)
                {
                    var falta = data - DateTimeOffset.UtcNow;
                    RetryAfter = falta < TimeSpan.Zero ? TimeSpan.Zero : falta;
                }
            }

            public int Status { get; }
            public string Corpo { get; }
            public bool TemCorpoJson => Corpo.TrimStart().StartsWith("{", StringComparison.Ordinal);
            public TimeSpan? RetryAfter { get; }
            public long? RateLimitLimit { get; }
            public long? RateLimitRemaining { get; }
            public long? RateLimitReset { get; }
            public long? QuotaLimit { get; }
            public long? QuotaRemaining { get; }
            public long? QuotaReset { get; }

            private static long? Numero(HttpResponseHeaders headers, string nome)
            {
                if (!headers.TryGetValues(nome, out var valores)) return null;
                var v = valores.FirstOrDefault();
                return long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (long?)null;
            }
        }
    }
}
