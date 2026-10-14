using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
#if NET8_0_OR_GREATER
using DataCivil = System.DateOnly;
#else
using DataCivil = System.DateTime;
#endif

namespace Apipj
{
    /// <summary>
    /// Documento do CNPJ no formato <c>completo</c> (padrão), como devolvido por <c>GET /v1/cnpj/{cnpj}</c>.
    /// Campos ausentes na base da Receita Federal vêm como <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Datas (<c>AAAA-MM-DD</c> na API) são <c>DateOnly</c> no .NET 8+ e <c>DateTime</c>
    /// (meia-noite, <c>DateTimeKind.Unspecified</c>) no .NET Standard 2.0 / .NET Framework.
    /// </remarks>
    public sealed record DocumentoCompleto
    {
        /// <summary>CNPJ com 14 caracteres, sem pontuação.</summary>
        [JsonPropertyName("cnpj")] public string Cnpj { get; set; } = string.Empty;

        /// <summary>CNPJ formatado (<c>AA.AAA.AAA/AAAA-DD</c>).</summary>
        [JsonPropertyName("cnpj_formatado")] public string CnpjFormatado { get; set; } = string.Empty;

        /// <summary>Razão social.</summary>
        [JsonPropertyName("razao_social")] public string RazaoSocial { get; set; } = string.Empty;

        /// <summary>Nome fantasia.</summary>
        [JsonPropertyName("nome_fantasia")] public string? NomeFantasia { get; set; }

        /// <summary><c>MATRIZ</c> ou <c>FILIAL</c>.</summary>
        [JsonPropertyName("matriz_filial")] public string? MatrizFilial { get; set; }

        /// <summary>Situação cadastral (<c>ATIVA</c>, <c>BAIXADA</c>, <c>INAPTA</c>, <c>SUSPENSA</c> ou <c>NULA</c>), data e motivo.</summary>
        [JsonPropertyName("situacao_cadastral")] public SituacaoCadastral SituacaoCadastral { get; set; } = new SituacaoCadastral();

        /// <summary>Situação especial e data.</summary>
        [JsonPropertyName("situacao_especial")] public SituacaoEspecial? SituacaoEspecial { get; set; }

        /// <summary>Data de início da atividade.</summary>
        [JsonPropertyName("data_inicio_atividade")] public DataCivil? DataInicioAtividade { get; set; }

        /// <summary>Natureza jurídica (código e descrição).</summary>
        [JsonPropertyName("natureza_juridica")] public CodigoDescricao? NaturezaJuridica { get; set; }

        /// <summary>Qualificação do responsável.</summary>
        [JsonPropertyName("qualificacao_responsavel")] public CodigoDescricao? QualificacaoResponsavel { get; set; }

        /// <summary>Porte da empresa.</summary>
        [JsonPropertyName("porte")] public CodigoDescricao? Porte { get; set; }

        /// <summary>Capital social, em reais.</summary>
        [JsonPropertyName("capital_social")] public decimal? CapitalSocial { get; set; }

        /// <summary>CNAE principal (código com 7 dígitos e descrição).</summary>
        [JsonPropertyName("cnae_principal")] public CodigoDescricao? CnaePrincipal { get; set; }

        /// <summary>CNAEs secundários.</summary>
        [JsonPropertyName("cnaes_secundarios")] public IReadOnlyList<CodigoDescricao> CnaesSecundarios { get; set; } = Array.Empty<CodigoDescricao>();

        /// <summary>Endereço do estabelecimento.</summary>
        [JsonPropertyName("endereco")] public Endereco Endereco { get; set; } = new Endereco();

        /// <summary>Telefones (DDD e número).</summary>
        [JsonPropertyName("telefones")] public IReadOnlyList<Telefone> Telefones { get; set; } = Array.Empty<Telefone>();

        /// <summary>Fax.</summary>
        [JsonPropertyName("fax")] public Telefone? Fax { get; set; }

        /// <summary>E-mail cadastrado na Receita.</summary>
        [JsonPropertyName("email")] public string? Email { get; set; }

        /// <summary>Ente federativo responsável (órgãos públicos).</summary>
        [JsonPropertyName("ente_federativo_responsavel")] public string? EnteFederativoResponsavel { get; set; }

        /// <summary>Opção pelo Simples Nacional.</summary>
        [JsonPropertyName("simples")] public OpcaoRegime? Simples { get; set; }

        /// <summary>Opção pelo MEI (SIMEI).</summary>
        [JsonPropertyName("mei")] public OpcaoRegime? Mei { get; set; }

        /// <summary>Quadro de sócios e administradores (QSA).</summary>
        [JsonPropertyName("socios")] public IReadOnlyList<Socio> Socios { get; set; } = Array.Empty<Socio>();

        /// <summary>Mês do lote da Receita Federal de onde veio o documento (<c>AAAA-MM</c>).</summary>
        [JsonPropertyName("atualizado_em")] public string AtualizadoEm { get; set; } = string.Empty;

        /// <summary>Campos que a API passar a devolver e que esta versão do pacote ainda não modela.</summary>
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }
    }

    /// <summary>Par código/descrição da Receita Federal (natureza jurídica, CNAE, porte, qualificação...).</summary>
    public sealed record CodigoDescricao
    {
        /// <summary>Código da Receita Federal.</summary>
        [JsonPropertyName("codigo")] public string? Codigo { get; set; }

        /// <summary>Descrição.</summary>
        [JsonPropertyName("descricao")] public string? Descricao { get; set; }
    }

    /// <summary>Situação cadastral do CNPJ.</summary>
    public sealed record SituacaoCadastral
    {
        /// <summary>Código (<c>02</c> = ATIVA, <c>08</c> = BAIXADA...).</summary>
        [JsonPropertyName("codigo")] public string? Codigo { get; set; }

        /// <summary><c>ATIVA</c>, <c>BAIXADA</c>, <c>INAPTA</c>, <c>SUSPENSA</c> ou <c>NULA</c>.</summary>
        [JsonPropertyName("descricao")] public string? Descricao { get; set; }

        /// <summary>Data da situação.</summary>
        [JsonPropertyName("data")] public DataCivil? Data { get; set; }

        /// <summary>Motivo da situação.</summary>
        [JsonPropertyName("motivo")] public CodigoDescricao? Motivo { get; set; }
    }

    /// <summary>Situação especial do CNPJ (recuperação judicial, falência...).</summary>
    public sealed record SituacaoEspecial
    {
        /// <summary>Descrição da situação especial.</summary>
        [JsonPropertyName("descricao")] public string? Descricao { get; set; }

        /// <summary>Data da situação especial.</summary>
        [JsonPropertyName("data")] public DataCivil? Data { get; set; }
    }

    /// <summary>Endereço do estabelecimento.</summary>
    public sealed record Endereco
    {
        /// <summary>Tipo de logradouro (RUA, AVENIDA...).</summary>
        [JsonPropertyName("tipo_logradouro")] public string? TipoLogradouro { get; set; }

        /// <summary>Logradouro.</summary>
        [JsonPropertyName("logradouro")] public string? Logradouro { get; set; }

        /// <summary>Número.</summary>
        [JsonPropertyName("numero")] public string? Numero { get; set; }

        /// <summary>Complemento.</summary>
        [JsonPropertyName("complemento")] public string? Complemento { get; set; }

        /// <summary>Bairro.</summary>
        [JsonPropertyName("bairro")] public string? Bairro { get; set; }

        /// <summary>CEP, só dígitos.</summary>
        [JsonPropertyName("cep")] public string? Cep { get; set; }

        /// <summary>Município (códigos da Receita e do IBGE e nome).</summary>
        [JsonPropertyName("municipio")] public Municipio Municipio { get; set; } = new Municipio();

        /// <summary>UF (sigla).</summary>
        [JsonPropertyName("uf")] public string? Uf { get; set; }

        /// <summary>País (endereço no exterior).</summary>
        [JsonPropertyName("pais")] public string? Pais { get; set; }

        /// <summary>Cidade no exterior.</summary>
        [JsonPropertyName("cidade_exterior")] public string? CidadeExterior { get; set; }
    }

    /// <summary>Município do endereço.</summary>
    public sealed record Municipio
    {
        /// <summary>Código do município na Receita Federal (tabela TOM/SIAFI).</summary>
        [JsonPropertyName("codigo_rfb")] public string? CodigoRfb { get; set; }

        /// <summary>Código do município no IBGE (7 dígitos).</summary>
        [JsonPropertyName("codigo_ibge")] public string? CodigoIbge { get; set; }

        /// <summary>Nome do município.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }
    }

    /// <summary>Telefone ou fax.</summary>
    public sealed record Telefone
    {
        /// <summary>DDD.</summary>
        [JsonPropertyName("ddd")] public string? Ddd { get; set; }

        /// <summary>Número, só dígitos.</summary>
        [JsonPropertyName("numero")] public string? Numero { get; set; }
    }

    /// <summary>Opção por regime (Simples Nacional ou MEI).</summary>
    public sealed record OpcaoRegime
    {
        /// <summary><c>true</c> se é optante hoje.</summary>
        [JsonPropertyName("optante")] public bool Optante { get; set; }

        /// <summary>Data da opção.</summary>
        [JsonPropertyName("data_opcao")] public DataCivil? DataOpcao { get; set; }

        /// <summary>Data da exclusão.</summary>
        [JsonPropertyName("data_exclusao")] public DataCivil? DataExclusao { get; set; }
    }

    /// <summary>Sócio ou administrador (QSA).</summary>
    public sealed record Socio
    {
        /// <summary>Nome do sócio.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }

        /// <summary><c>PESSOA FISICA</c>, <c>PESSOA JURIDICA</c> ou <c>ESTRANGEIRO</c>.</summary>
        [JsonPropertyName("tipo")] public string? Tipo { get; set; }

        /// <summary>CPF já mascarado pela Receita (<c>***123456**</c>) ou raiz de 8 caracteres do CNPJ do sócio pessoa jurídica.</summary>
        [JsonPropertyName("cpf_cnpj")] public string? CpfCnpj { get; set; }

        /// <summary>Qualificação do sócio (Diretor, Sócio-Administrador...).</summary>
        [JsonPropertyName("qualificacao")] public CodigoDescricao? Qualificacao { get; set; }

        /// <summary>Data de entrada na sociedade.</summary>
        [JsonPropertyName("data_entrada")] public DataCivil? DataEntrada { get; set; }

        /// <summary>Faixa etária.</summary>
        [JsonPropertyName("faixa_etaria")] public string? FaixaEtaria { get; set; }

        /// <summary>País (sócio estrangeiro).</summary>
        [JsonPropertyName("pais")] public string? Pais { get; set; }

        /// <summary>Representante legal (sócio menor, incapaz ou estrangeiro).</summary>
        [JsonPropertyName("representante_legal")] public RepresentanteLegal? RepresentanteLegal { get; set; }
    }

    /// <summary>Representante legal de um sócio.</summary>
    public sealed record RepresentanteLegal
    {
        /// <summary>CPF mascarado.</summary>
        [JsonPropertyName("cpf")] public string? Cpf { get; set; }

        /// <summary>Nome.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }

        /// <summary>Qualificação do representante.</summary>
        [JsonPropertyName("qualificacao")] public CodigoDescricao? Qualificacao { get; set; }
    }

    /// <summary>Resultado da validação de CNPJ (<c>GET /v1/valida/{cnpj}</c> ou <see cref="Apipj.Cnpj.ValidarLocal"/>).</summary>
    public sealed record Validacao
    {
        /// <summary>Entrada normalizada (sem pontuação, maiúsculas).</summary>
        [JsonPropertyName("cnpj")] public string Cnpj { get; set; } = string.Empty;

        /// <summary><c>true</c> se formato e dígitos verificadores são válidos.</summary>
        [JsonPropertyName("valido")] public bool Valido { get; set; }

        /// <summary><c>AA.AAA.AAA/AAAA-DD</c> quando válido.</summary>
        [JsonPropertyName("formatado")] public string? Formatado { get; set; }

        /// <summary><c>true</c> se a raiz ou a ordem têm letras (CNPJ alfanumérico).</summary>
        [JsonPropertyName("alfanumerico")] public bool Alfanumerico { get; set; }

        /// <summary>Motivo quando inválido.</summary>
        [JsonPropertyName("erro")] public string? Erro { get; set; }
    }

    /// <summary>Status do serviço e da base (<c>GET /v1/status</c>).</summary>
    public sealed record StatusBase
    {
        /// <summary><c>OK</c> ou <c>DEGRADED</c> (base indisponível: a consulta responde 503).</summary>
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;

        /// <summary>Mês do lote da Receita Federal carregado (<c>AAAA-MM</c>).</summary>
        [JsonPropertyName("dataset_month")] public string? DatasetMonth { get; set; }

        /// <summary>Quando a base foi gerada.</summary>
        [JsonPropertyName("dataset_generated_at")] public DateTimeOffset? DatasetGeneratedAt { get; set; }

        /// <summary>Quantidade de CNPJs na base.</summary>
        [JsonPropertyName("rows")] public long? Rows { get; set; }

        /// <summary>Versão da API.</summary>
        [JsonPropertyName("version")] public string? Version { get; set; }

        /// <summary>Hora do servidor.</summary>
        [JsonPropertyName("time")] public DateTimeOffset? Time { get; set; }
    }

    /// <summary>Plano, limites e uso do mês da chave (<c>GET /v1/usage</c>, não consome cota).</summary>
    public sealed record Uso
    {
        /// <summary>Identificador do plano (<c>free</c>, <c>dev</c>, <c>pro</c>...).</summary>
        [JsonPropertyName("plan")] public string Plan { get; set; } = string.Empty;

        /// <summary>Nome do plano.</summary>
        [JsonPropertyName("plan_name")] public string? PlanName { get; set; }

        /// <summary>Preço mensal do plano, em reais.</summary>
        [JsonPropertyName("plan_price")] public decimal? PlanPrice { get; set; }

        /// <summary>Validade do plano pago, ou <c>null</c>.</summary>
        [JsonPropertyName("plan_valid_until")] public DateTimeOffset? PlanValidUntil { get; set; }

        /// <summary>Limites do plano.</summary>
        [JsonPropertyName("limits")] public LimitesPlano? Limits { get; set; }

        /// <summary>Consultas usadas no mês.</summary>
        [JsonPropertyName("used")] public long Used { get; set; }

        /// <summary>Cota mensal do plano.</summary>
        [JsonPropertyName("quota")] public long Quota { get; set; }

        /// <summary>Consultas restantes no mês.</summary>
        [JsonPropertyName("remaining")] public long Remaining { get; set; }

        /// <summary>Epoch Unix (segundos) em que a cota zera.</summary>
        [JsonPropertyName("reset")] public long? Reset { get; set; }

        /// <summary>Quando a cota zera.</summary>
        [JsonPropertyName("reset_at")] public DateTimeOffset? ResetAt { get; set; }

        /// <summary>Mês de referência (<c>AAAA-MM</c>).</summary>
        [JsonPropertyName("period")] public string? Period { get; set; }
    }

    /// <summary>Limites de um plano.</summary>
    public sealed record LimitesPlano
    {
        /// <summary>Consultas por minuto.</summary>
        [JsonPropertyName("rpm")] public int Rpm { get; set; }

        /// <summary>Consultas por mês.</summary>
        [JsonPropertyName("monthly_quota")] public long MonthlyQuota { get; set; }
    }

    /// <summary>Um item de <see cref="ApipjClient.ConsultarVariosAsync"/>.</summary>
    public sealed record ResultadoConsulta
    {
        /// <summary>CNPJ normalizado.</summary>
        public string Cnpj { get; init; } = string.Empty;

        /// <summary>O documento, ou <c>null</c> se o CNPJ não está na base (404) ou houve erro.</summary>
        public DocumentoCompleto? Documento { get; init; }

        /// <summary>Erro deste CNPJ (<c>invalid_cnpj</c>, 5xx ou rede depois das tentativas...), ou <c>null</c>.</summary>
        public ApipjException? Erro { get; init; }
    }

    /// <summary>Limites informados pela API nos cabeçalhos <c>X-RateLimit-*</c> e <c>X-Quota-*</c> da última consulta.</summary>
    public sealed record Limites
    {
        /// <summary>Consultas por minuto do plano (<c>X-RateLimit-Limit</c>).</summary>
        public long? PorMinutoLimite { get; init; }

        /// <summary>Consultas restantes no minuto (<c>X-RateLimit-Remaining</c>).</summary>
        public long? PorMinutoRestante { get; init; }

        /// <summary>Segundos até a janela por minuto reabrir (<c>X-RateLimit-Reset</c>).</summary>
        public long? PorMinutoReset { get; init; }

        /// <summary>Cota mensal (<c>X-Quota-Limit</c>).</summary>
        public long? MensalLimite { get; init; }

        /// <summary>Consultas restantes no mês (<c>X-Quota-Remaining</c>).</summary>
        public long? MensalRestante { get; init; }

        /// <summary>Epoch Unix (segundos) em que a cota mensal zera (<c>X-Quota-Reset</c>).</summary>
        public long? MensalReset { get; init; }
    }

    /// <summary>Lê datas <c>AAAA-MM-DD</c>; valor vazio ou fora do formato vira <c>null</c> em vez de quebrar a desserialização.</summary>
    internal sealed class DataConverter : JsonConverter<DataCivil?>
    {
        public override DataCivil? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) { reader.Skip(); return null; }
            var s = reader.GetString();
            if (string.IsNullOrEmpty(s) || s!.Length < 10) return null;
#if NET8_0_OR_GREATER
            return DateOnly.TryParseExact(s.AsSpan(0, 10), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;
#else
            return DateTime.TryParseExact(s.Substring(0, 10), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : (DateTime?)null;
#endif
        }

        public override void Write(Utf8JsonWriter writer, DataCivil? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Corpo padrão de erro da API.</summary>
    internal sealed class CorpoErro
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("retry_after")] public double? RetryAfter { get; set; }
        [JsonPropertyName("upgrade_url")] public string? UpgradeUrl { get; set; }
        [JsonPropertyName("reset")] public long? Reset { get; set; }
        [JsonPropertyName("cnpj")] public string? Cnpj { get; set; }
    }

    [JsonSourceGenerationOptions(Converters = new[] { typeof(DataConverter) })]
    [JsonSerializable(typeof(DocumentoCompleto))]
    [JsonSerializable(typeof(Validacao))]
    [JsonSerializable(typeof(StatusBase))]
    [JsonSerializable(typeof(Uso))]
    [JsonSerializable(typeof(CorpoErro))]
    internal sealed partial class ApipjJsonContext : JsonSerializerContext
    {
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill para <c>init</c> e <c>record</c> no .NET Standard 2.0.</summary>
    internal static class IsExternalInit
    {
    }
}
#endif
