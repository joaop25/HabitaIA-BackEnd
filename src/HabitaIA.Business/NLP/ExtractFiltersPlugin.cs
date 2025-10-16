using HabitaIA.Business.NLP.Interfaces;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.NLP
{
    public class ExtractFiltersPlugin
    {
        [KernelFunction("extract_filters")]
        [Description("Extrai filtros de busca de imóveis a partir do texto do usuário.")]
        public ExtractedFilters Extract(
            [Description("Preço máximo em reais. Exemplo: 300000 para R$ 300.000,00.")]
        decimal? precoMaximo = null,

            [Description("Quantidade mínima de quartos. Exemplo: 2.")]
        int? quartosMinimos = null,

            [Description("Nome do bairro (apenas o bairro, sem cidade/UF).")]
        string? bairro = null,

            [Description("Quantidade de resultados desejada (Top-N).")]
        int? limite = null
        )
        {
            // Aqui normalmente você só retorna os argumentos que o modelo passou.
            // (Pode normalizar 'bairro' ou aplicar caps de limite, se quiser)
            var b = string.IsNullOrWhiteSpace(bairro) ? null : NormalizeBairro(bairro);
            var l = limite is int n ? Math.Clamp(n, 1, 100) : (int?)null;

            return new ExtractedFilters(precoMaximo, quartosMinimos, b, l);
        }

        private static string NormalizeBairro(string s)
            => string.Join(' ', s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));
    }
}
