# apipj: API CNPJ gratuita para .NET (C#)

[![NuGet](https://img.shields.io/nuget/v/Apipj.svg)](https://www.nuget.org/packages/Apipj)

**API para consultar CNPJ** em C# e .NET: o cliente oficial da **API de CNPJ do [apipj](https://apipj.com.br)**, com **plano gratuito** de 50 mil consultas por mês, sem cartão. Consulte CNPJ e receba razão social, situação cadastral, endereço, CNAEs, Simples/MEI e sócios a partir dos dados abertos da Receita Federal, com suporte ao **CNPJ alfanumérico**.

- .NET 8+ e .NET Standard 2.0 (.NET Framework 4.6.2+, .NET Core 3.1+). Sem dependências no .NET 8; no .NET Standard 2.0, só `System.Text.Json` (e o `Microsoft.Bcl.AsyncInterfaces` que ele já usa).
- Records tipados (`DocumentoCompleto`, `Socio`, `Endereco`...), com `System.Text.Json` gerado em compilação: compatível com trimming e Native AOT.
- Valida o CNPJ (inclusive alfanumérico) localmente antes de chamar a API: CNPJ inválido não gasta cota.
- Repete sozinho em limite por minuto (`429` com `Retry-After`), erro `5xx`, falha de rede e tempo esgotado.
- Consulta em lote com `ConsultarVariosAsync`, respeitando o limite por minuto do seu plano.

> **Atenção: a chave de API é secreta.** Use este pacote só no servidor (ASP.NET Core, workers, Azure Functions, scripts). **Nunca** coloque a chave em apps desktop/mobile distribuídos ou em Blazor WebAssembly: qualquer pessoa conseguiria copiá-la e gastar a sua cota. Se o front-end precisa de dados de CNPJ, crie um endpoint no seu back-end que chama a API.

## Instalação

```bash
dotnet add package Apipj
```

Crie uma conta grátis em [apipj.com.br/criar-conta](https://apipj.com.br/criar-conta), gere a chave no painel e deixe-a fora do código (variável de ambiente, Secret Manager ou cofre de segredos):

```bash
export APIPJ_KEY=apipj_live_...
# ou, em desenvolvimento no ASP.NET Core:
dotnet user-secrets set APIPJ_KEY "apipj_live_..."
```

## Consultar CNPJ

```csharp
using Apipj;

using var api = new ApipjClient(); // lê APIPJ_KEY; ou new ApipjClient(apiKey: "apipj_live_...")

// Documento completo, ou null se o CNPJ não está na base da Receita (404)
var empresa = await api.ConsultarAsync("00.000.000/0001-91");
if (empresa is not null)
{
    Console.WriteLine(empresa.RazaoSocial);                    // BANCO DO BRASIL SA
    Console.WriteLine(empresa.SituacaoCadastral.Descricao);    // ATIVA
    Console.WriteLine(empresa.DataInicioAtividade?.ToString("yyyy-MM-dd")); // 1966-08-01 (DateOnly no .NET 8)
    Console.WriteLine(empresa.Endereco.Municipio.CodigoIbge);  // 5300108
    Console.WriteLine(empresa.CnaePrincipal?.Descricao);       // Bancos múltiplos, com carteira comercial
    foreach (var socio in empresa.Socios)
        Console.WriteLine($"{socio.Nome} ({socio.Qualificacao?.Descricao})");
}

// CNPJ que perdeu os zeros à esquerda (planilha, campo numérico) é completado: "191" vira "00000000000191"
var bb = await api.ConsultarAsync("191");

// Formato simplificado: plano, com datas DD/MM/AAAA e CNPJ, CEP e telefone já formatados
using var simples = await api.ConsultarSimplificadoAsync("00000000000191");
Console.WriteLine(simples?.RootElement.GetProperty("nome").GetString());

// Validação pela API (GET /v1/valida: sem chave e sem consumo de cota)
var v = await api.ValidarAsync("12.ABC.345/01DE-35"); // Valido = true, Alfanumerico = true

// Status do serviço e mês do lote da Receita carregado (sem chave)
var status = await api.StatusAsync();                 // Status = "OK", DatasetMonth = "2026-09"

// Plano, limites e uso do mês da sua chave (não consome cota)
var uso = await api.UsoAsync();                       // Plan, Used, Quota, Remaining, ResetAt
```

O construtor é `new ApipjClient(HttpClient? http = null, string? apiKey = null, string? baseUrl = null)`. Reaproveite uma instância (ela é thread-safe). Ajustes opcionais: `api.MaxRetentativas` (padrão 3) e `api.TimeoutPorTentativa` (padrão 10 s). Depois de cada consulta, `api.Limites` traz o que a API informou nos cabeçalhos (`PorMinutoLimite`, `PorMinutoRestante`, `MensalLimite`, `MensalRestante`...).

### Validar CNPJ (inclusive alfanumérico), offline e sem chamar a API

```csharp
Cnpj.Valido("12.ABC.345/01DE-35");      // true  (alfanumérico)
Cnpj.Valido("12.ABC.345/01DE-36");      // false (dígito verificador errado)
Cnpj.Valido("00.000.000/0000-00");      // false (zerado)
Cnpj.Normalizar("12.abc.345/01de-35");  // "12ABC34501DE35"
Cnpj.Normalizar("191");                 // "00000000000191"
Cnpj.Formatar("11378117000120");        // "11.378.117/0001-20"
Cnpj.ValidarLocal("12ABC34501DE36");    // Validacao { Valido = false, Erro = "Dígitos verificadores inválidos" }
```

`Cnpj.Valido` confere formato e dígitos verificadores (numérico e alfanumérico, IN RFB 2.229/2024); não diz se o CNPJ existe. `Cnpj.CalcularDv` calcula os dois dígitos de uma base de 12 caracteres.

## No ASP.NET Core (injeção de dependência)

Registre o cliente pela fábrica de `HttpClient` (`IHttpClientFactory`), sem pacote extra: o ASP.NET Core já traz `AddHttpClient`.

```csharp
using Apipj;

var builder = WebApplication.CreateBuilder(args);

// A chave vem da configuração: variável de ambiente APIPJ_KEY, user-secrets ou appsettings
builder.Services.AddHttpClient("apipj")
    .AddTypedClient((http, sp) => new ApipjClient(http, sp.GetRequiredService<IConfiguration>()["APIPJ_KEY"]));

var app = builder.Build();

app.MapGet("/clientes/cnpj/{cnpj}", async (string cnpj, ApipjClient apipj, CancellationToken ct) =>
{
    if (!Cnpj.Valido(cnpj)) return Results.BadRequest(new { erro = "CNPJ inválido" });
    var empresa = await apipj.ConsultarAsync(cnpj, ct);
    return empresa is null ? Results.NotFound() : Results.Ok(empresa);
});

app.Run();
```

Se a chave está só na variável de ambiente `APIPJ_KEY`, basta `builder.Services.AddHttpClient<ApipjClient>();`. Num worker ou console sem ASP.NET Core, o mesmo registro funciona com o pacote `Microsoft.Extensions.Http`.

A base da Receita muda uma vez por mês: guardar as respostas em `IMemoryCache` ou `IDistributedCache` por até 30 dias é seguro para cadastro e economiza cota (veja o [guia de C#](https://apipj.com.br/guias/consultar-cnpj-csharp)). Na rota, receba o CNPJ sem a barra da máscara (`11378117000120`) ou por query string.

## Consultar CNPJ em lote

```csharp
var lista = new[] { "11.378.117/0001-20", "00.000.000/0001-91", "11378117000120", "123" };

await foreach (var r in api.ConsultarVariosAsync(lista, simultaneas: 4))
{
    // na ordem de conclusão
    if (r.Erro is not null) Console.WriteLine($"{r.Cnpj} {r.Erro.Code}: {r.Erro.Message}");
    else if (r.Documento is null) Console.WriteLine($"{r.Cnpj} não encontrado");
    else Console.WriteLine($"{r.Cnpj} {r.Documento.RazaoSocial}");
}
```

- Lê o limite por minuto do seu plano dos cabeçalhos `X-RateLimit-*` e **nunca passa dele** (a primeira consulta sai sozinha para descobrir o limite).
- CNPJs repetidos (mesmo com máscaras diferentes) são consultados uma vez só.
- CNPJs inválidos **não chamam a API**: saem com `Documento = null` e `Erro.Code == "invalid_cnpj"`.
- Erro de um CNPJ (por exemplo `5xx` depois das tentativas) sai no próprio item e o lote continua.
- `401` (chave inválida) e cota mensal esgotada (`quota_exceeded`) interrompem o lote lançando `ApipjException`, depois de entregar os resultados já prontos.
- Um `break` no `await foreach` (ou o `CancellationToken`) cancela as consultas em andamento.

## Tratamento de erros

Todos os erros da API viram `ApipjException`, com `StatusCode` (HTTP, ou `0` para rede/tempo esgotado), `Code` (estável, em snake_case) e `Message` (em português).

```csharp
try
{
    var empresa = await api.ConsultarAsync(cnpj);
    if (empresa is null) Console.WriteLine("CNPJ não encontrado na base da Receita");
}
catch (ApipjException e)
{
    switch (e.Code)
    {
        case "invalid_cnpj":   // 400: formato ou dígito verificador inválido (verificado antes de chamar a API)
            break;
        case "unauthorized":   // 401: chave ausente, inválida ou revogada
            break;
        case "quota_exceeded": // 429: cota mensal esgotada; a mensagem traz os links para assinar Dev ou Pro; e.Reset é quando zera
            break;
        case "rate_limited":   // 429: limite por minuto, mesmo depois de esperar Retry-After em todas as tentativas; e.RetryAfter
            break;
        default:               // 5xx (dataset_unavailable, server_error), network_error, timeout
            Console.WriteLine($"{e.StatusCode} {e.Code}: {e.Message}");
            break;
    }
}
```

| Situação | O que o cliente faz |
|---|---|
| CNPJ inválido | lança `ApipjException(400, "invalid_cnpj")` **sem chamar a API** |
| `404` (CNPJ válido, fora da base) | devolve `null` |
| `400`, `401` | lança na hora |
| `429 quota_exceeded` | lança na hora, com links de assinatura, `UpgradeUrl` e `Reset` |
| `429 rate_limited` | espera `Retry-After` e repete (até 3 vezes); depois lança com `RetryAfter` |
| `5xx`, falha de rede, tempo esgotado | repete com espera de 1 s, 2 s e 4 s; depois lança |

## Tipos e datas

Os records espelham o JSON da API (`razao_social` → `RazaoSocial`), com `[JsonPropertyName]` em cada campo: `JsonSerializer.Serialize(empresa)` devolve o mesmo formato da API. Campos ausentes na base da Receita vêm como `null`.

Datas (`AAAA-MM-DD` na API) são `DateOnly?` no .NET 8+ e `DateTime?` (meia-noite) no .NET Standard 2.0, que não tem `DateOnly`. Data vazia ou fora do formato vira `null` em vez de quebrar a desserialização. `AtualizadoEm` (mês do lote, `AAAA-MM`) é `string`. Campos novos que a API passar a devolver ficam em `DocumentoCompleto.Extras`.

## Limites e plano gratuito

Cada consulta respondida (inclusive `404`) conta 1 na cota mensal; também vale um limite de consultas por minuto por chave. `ValidarAsync`, `StatusAsync` e `UsoAsync` não consomem cota.

| Plano | Consultas por minuto | Consultas por mês | Preço | |
|---|---|---|---|---|
| Gratuito | 10 | 50.000 | R$ 0 | [Criar conta](https://apipj.com.br/criar-conta) |
| Dev | 30 | 200.000 | R$ 12,90/mês | [Assinar Dev](https://apipj.com.br/painel?assinar=dev) |
| Pro | 200 | 600.000 | R$ 59,90/mês | [Assinar Pro](https://apipj.com.br/painel?assinar=pro) |

Valores atuais em [apipj.com.br/precos](https://apipj.com.br/precos).

## Links

- Documentação da API: [apipj.com.br/docs](https://apipj.com.br/docs)
- OpenAPI: [apipj.com.br/openapi-publica.json](https://apipj.com.br/openapi-publica.json)
- Guia de C#: [apipj.com.br/guias/consultar-cnpj-csharp](https://apipj.com.br/guias/consultar-cnpj-csharp)
- Outras linguagens: [JavaScript/TypeScript](https://github.com/apipj/apipj-js) e [Python](https://github.com/apipj/apipj-py)

---

## English

**Apipj** is the official .NET client for [apipj](https://apipj.com.br), a **free Brazilian CNPJ lookup API** (company registry data from the Receita Federal open data: legal name, registration status, address, CNAE activity codes, Simples/MEI tax regime and partners). The free plan includes 50,000 lookups per month, no credit card required. Supports the new **alphanumeric CNPJ**.

```bash
dotnet add package Apipj
```

```csharp
using Apipj;

using var api = new ApipjClient(apiKey: Environment.GetEnvironmentVariable("APIPJ_KEY"));
var company = await api.ConsultarAsync("00.000.000/0001-91"); // null when the CNPJ is not in the registry
Console.WriteLine(company?.RazaoSocial);                    // BANCO DO BRASIL SA

bool ok = Cnpj.Valido("12.ABC.345/01DE-35");                // offline check-digit validation (numeric and alphanumeric)

await foreach (var r in api.ConsultarVariosAsync(new[] { "11378117000120", "00000000000191" }))
    Console.WriteLine($"{r.Cnpj} {r.Documento?.RazaoSocial ?? r.Erro?.Code}");
```

- Targets .NET 8+ (no dependencies) and .NET Standard 2.0 (`System.Text.Json` only). Source-generated JSON (trimming/AOT friendly).
- Validates the CNPJ locally before calling the API (invalid input never costs quota); pads numeric input that lost leading zeros.
- `404` returns `null`; `429 rate_limited` waits for `Retry-After` and retries; `5xx`/network errors retry with 1 s, 2 s, 4 s backoff; `quota_exceeded` throws immediately.
- Errors are `ApipjException` with `StatusCode` and a stable `Code` (`invalid_cnpj`, `unauthorized`, `quota_exceeded`, `rate_limited`...).
- ASP.NET Core: `services.AddHttpClient("apipj").AddTypedClient((http, sp) => new ApipjClient(http, sp.GetRequiredService<IConfiguration>()["APIPJ_KEY"]))`.
- Method names are in Portuguese, matching the API fields: `ConsultarAsync` (lookup), `ConsultarVariosAsync` (batch), `ValidarAsync` (validate), `StatusAsync`, `UsoAsync` (usage).

Sign up for a free key at [apipj.com.br/criar-conta](https://apipj.com.br/criar-conta). Docs: [apipj.com.br/docs](https://apipj.com.br/docs).

## Licença / License

[MIT](https://github.com/apipj/apipj-dotnet/blob/main/LICENSE). Os dados vêm dos dados abertos do CNPJ publicados pela Receita Federal.
