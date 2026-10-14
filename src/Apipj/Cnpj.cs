using System;
using System.Text;

namespace Apipj
{
    /// <summary>
    /// Validação, normalização e formatação de CNPJ, 100% local (sem rede).
    /// </summary>
    /// <remarks>
    /// Vale para o CNPJ numérico e para o alfanumérico (IN RFB 2.229/2024):
    /// <list type="bullet">
    /// <item>14 caracteres: posições 1 a 12 em [A-Z0-9], posições 13 e 14 (dígitos verificadores) numéricas;</item>
    /// <item>valor de cada caractere = código ASCII - 48 ('0'..'9' = 0..9, 'A'..'Z' = 17..42);</item>
    /// <item>DV1 com pesos 5,4,3,2,9,8,7,6,5,4,3,2 e DV2 com pesos 6,5,4,3,2,9,8,7,6,5,4,3,2;</item>
    /// <item>resto = soma % 11; DV = 0 se resto &lt; 2, senão 11 - resto;</item>
    /// <item>"00000000000000" é inválido mesmo com o DV "certo".</item>
    /// </list>
    /// </remarks>
    public static class Cnpj
    {
        private static readonly int[] Pesos = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        /// <summary>
        /// Remove pontuação (<c>.</c>, <c>/</c>, <c>-</c>) e espaços e converte para maiúsculas.
        /// Entrada só com dígitos e menos de 14 caracteres ganha zeros à esquerda
        /// (CNPJ que perdeu os zeros iniciais ao passar por planilha ou campo numérico: <c>191</c> vira <c>00000000000191</c>).
        /// Não valida.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        /// <returns>CNPJ normalizado (pode ter tamanho diferente de 14 se a entrada for inválida); vazio para <c>null</c>.</returns>
        /// <example><code>Cnpj.Normalizar("12.abc.345/01de-35"); // "12ABC34501DE35"</code></example>
        public static string Normalizar(string? cnpj)
        {
            if (cnpj is null) return string.Empty;
            var sb = new StringBuilder(cnpj.Length);
            var soDigitos = true;
            foreach (var ch in cnpj)
            {
                if (ch == '.' || ch == '/' || ch == '-' || char.IsWhiteSpace(ch)) continue;
                var c = char.ToUpperInvariant(ch);
                if (c < '0' || c > '9') soDigitos = false;
                sb.Append(c);
            }
            if (soDigitos && sb.Length > 0 && sb.Length < 14) sb.Insert(0, "0", 14 - sb.Length);
            return sb.ToString();
        }

        /// <summary>
        /// Calcula os dois dígitos verificadores de uma base de 12 caracteres [A-Z0-9].
        /// </summary>
        /// <param name="base12">Raiz (8) + ordem (4), sem máscara.</param>
        /// <returns>Os dois dígitos, por exemplo <c>"35"</c>.</returns>
        /// <exception cref="ArgumentException">Se a base não tiver 12 caracteres [A-Z0-9].</exception>
        public static string CalcularDv(string base12)
        {
            if (base12 is null || base12.Length != 12) throw new ArgumentException("a base do CNPJ deve ter 12 caracteres [A-Z0-9]", nameof(base12));
            int s1 = 0, s2 = 0;
            for (var i = 0; i < 12; i++)
            {
                var ch = base12[i];
                if (!EhAlfanumerico(ch)) throw new ArgumentException("a base do CNPJ deve ter 12 caracteres [A-Z0-9]", nameof(base12));
                var v = ch - 48;
                s1 += v * Pesos[i + 1];
                s2 += v * Pesos[i];
            }
            var dv1 = s1 % 11 < 2 ? 0 : 11 - (s1 % 11);
            s2 += dv1 * Pesos[12];
            var dv2 = s2 % 11 < 2 ? 0 : 11 - (s2 % 11);
            return string.Concat(dv1.ToString(), dv2.ToString());
        }

        /// <summary>
        /// Validação local, com o mesmo formato de resposta de <c>GET /v1/valida/{cnpj}</c>.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        public static Validacao ValidarLocal(string? cnpj)
        {
            var c = Normalizar(cnpj);
            Validacao Invalido(string erro) => new Validacao { Cnpj = c, Valido = false, Formatado = null, Alfanumerico = false, Erro = erro };

            if (c.Length == 0) return Invalido("CNPJ vazio");
            if (c.Length != 14) return Invalido($"CNPJ deve ter 14 caracteres (recebido {c.Length})");
            for (var i = 0; i < 14; i++)
            {
                if (!EhAlfanumerico(c[i])) return Invalido("CNPJ contém caracteres inválidos (permitidos: letras A-Z e dígitos)");
            }
            if (!EhDigito(c[12]) || !EhDigito(c[13])) return Invalido("Os dois últimos caracteres (dígitos verificadores) devem ser numéricos");
            if (c == "00000000000000") return Invalido("CNPJ zerado é inválido");
            if (c.Substring(12) != CalcularDv(c.Substring(0, 12))) return Invalido("Dígitos verificadores inválidos");

            var alfa = false;
            for (var i = 0; i < 12; i++) alfa |= c[i] >= 'A' && c[i] <= 'Z';
            return new Validacao { Cnpj = c, Valido = true, Formatado = Formatar(c), Alfanumerico = alfa, Erro = null };
        }

        /// <summary>
        /// <c>true</c> se o CNPJ (numérico ou alfanumérico, com ou sem máscara) tem formato e dígitos verificadores válidos.
        /// Local e offline: não consulta a API nem diz se o CNPJ existe na base da Receita Federal.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        /// <example><code>Cnpj.Valido("12.ABC.345/01DE-35"); // true</code></example>
        public static bool Valido(string? cnpj) => ValidarLocal(cnpj).Valido;

        /// <summary>
        /// Formata como <c>AA.AAA.AAA/AAAA-DD</c>. Se a entrada normalizada não tiver 14 caracteres,
        /// devolve a entrada normalizada, sem máscara. Não confere os dígitos verificadores.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        /// <example><code>Cnpj.Formatar("11378117000120"); // "11.378.117/0001-20"</code></example>
        public static string Formatar(string? cnpj)
        {
            var c = Normalizar(cnpj);
            if (c.Length != 14) return c;
            return $"{c.Substring(0, 2)}.{c.Substring(2, 3)}.{c.Substring(5, 3)}/{c.Substring(8, 4)}-{c.Substring(12, 2)}";
        }

        private static bool EhDigito(char c) => c >= '0' && c <= '9';

        private static bool EhAlfanumerico(char c) => EhDigito(c) || (c >= 'A' && c <= 'Z');
    }
}
