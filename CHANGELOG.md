# Changelog

Todas as mudanças relevantes deste pacote. Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões seguem [SemVer](https://semver.org/lang/pt-BR/).

## [0.1.0] - 2026-10-13

Primeira versão.

### Adicionado

- `ApipjClient` com `ConsultarAsync` (formato completo, tipado em `DocumentoCompleto`; `404` vira `null`), `ConsultarSimplificadoAsync` (`JsonDocument`), `ConsultarVariosAsync` (lote com limite por minuto lido de `X-RateLimit-*`, deduplicação e CNPJs inválidos pulados sem chamar a API), `ValidarAsync`, `StatusAsync` e `UsoAsync`.
- Repetição automática: `429 rate_limited` espera `Retry-After`; `5xx`, falha de rede e tempo esgotado com espera de 1 s, 2 s e 4 s (até 3 retentativas).
- `ApipjException` com `StatusCode`, `Code`, `RetryAfter`, `UpgradeUrl` e `Reset`; `quota_exceeded` traz os links para assinar os planos Dev e Pro.
- Utilitários offline: `Cnpj.Valido`, `Cnpj.Normalizar` (completa zeros à esquerda), `Cnpj.Formatar`, `Cnpj.ValidarLocal`, `Cnpj.CalcularDv` (inclusive CNPJ alfanumérico).
- Alvos `net8.0` (datas em `DateOnly`, compatível com trimming e Native AOT) e `netstandard2.0` (datas em `DateTime`).

[0.1.0]: https://github.com/apipj/apipj-dotnet/releases/tag/v0.1.0
