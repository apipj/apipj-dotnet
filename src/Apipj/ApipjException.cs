using System;

namespace Apipj
{
    /// <summary>
    /// Erro lançado pelo cliente apipj.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="StatusCode"/>: status HTTP da resposta (400, 401, 429, 5xx) ou <c>0</c> para falha de rede ou tempo esgotado.</item>
    /// <item><see cref="Code"/>: código estável em snake_case (<c>invalid_cnpj</c>, <c>unauthorized</c>, <c>rate_limited</c>,
    /// <c>quota_exceeded</c>, <c>dataset_unavailable</c>, <c>server_error</c>, <c>network_error</c>, <c>timeout</c>, <c>invalid_response</c>...).</item>
    /// <item><see cref="RetryAfter"/>: quanto esperar (só em <c>rate_limited</c>).</item>
    /// <item><see cref="UpgradeUrl"/> e <see cref="Reset"/>: página de planos e quando a cota zera (só em <c>quota_exceeded</c>).</item>
    /// </list>
    /// </remarks>
    public class ApipjException : Exception
    {
        /// <summary>Cria o erro.</summary>
        /// <param name="statusCode">Status HTTP, ou 0 para rede/tempo esgotado.</param>
        /// <param name="code">Código estável em snake_case.</param>
        /// <param name="message">Mensagem em português.</param>
        /// <param name="innerException">Causa, se houver.</param>
        public ApipjException(int statusCode, string code, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Code = code;
        }

        /// <summary>Status HTTP da resposta (400, 401, 429, 5xx) ou <c>0</c> para falha de rede ou tempo esgotado.</summary>
        public int StatusCode { get; }

        /// <summary>Código estável em snake_case, por exemplo <c>invalid_cnpj</c> ou <c>quota_exceeded</c>.</summary>
        public string Code { get; }

        /// <summary>Quanto esperar antes de tentar de novo (só em <c>rate_limited</c>).</summary>
        public TimeSpan? RetryAfter { get; init; }

        /// <summary>Página de planos (só em <c>quota_exceeded</c>).</summary>
        public string? UpgradeUrl { get; init; }

        /// <summary>Quando a cota mensal zera (só em <c>quota_exceeded</c>).</summary>
        public DateTimeOffset? Reset { get; init; }

        /// <summary>CNPJ normalizado a que o erro se refere (em <c>invalid_cnpj</c>).</summary>
        public string? Cnpj { get; init; }

        /// <summary>Corpo bruto da resposta de erro, se houver.</summary>
        public string? Corpo { get; init; }
    }
}
