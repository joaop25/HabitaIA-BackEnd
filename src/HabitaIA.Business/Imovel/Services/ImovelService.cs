using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.Imovel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Services
{
    public class ImovelService : IImovelService
    {
        private readonly IImovelRepository _repo;
        private readonly IEmbeddingService _emb;

        public ImovelService(IImovelRepository repo, IEmbeddingService emb)
        {
            _repo = repo; _emb = emb;
        }

        private const double PesoSemantico = 0.75;
        private const double PesoFiltros = 0.25;

        public async Task<IReadOnlyList<ImovelScore>> BuscarAsync(BuscaImovelRequest requisicao, CancellationToken ct)
        {
            // 1) Embedding da consulta
            var consultaEmbedding = await _emb.GenerateAsync(requisicao.ConsultaLivre, ct);

            // 2) Candidatos (amostra maior)
            var amostra = await _repo.BuscarCandidatosAsync(
                requisicao.PrecoMaximo,
                requisicao.QuartosMinimos,
                requisicao.Bairro,
                take: Math.Max(requisicao.Limite * 5, 50),
                ct
            );

            if (amostra.Count == 0)
                return Array.Empty<ImovelScore>();

            var maiorQuartosEntreCandidatos = amostra.Max(i => i.Quartos);

            // 3) Ranking (similaridade + filtros)
            var ranqueados = amostra
                .Select(imovel =>
                {
                    var similaridadeCoseno = IEmbeddingService.Cosine(consultaEmbedding, imovel.Embedding ?? Array.Empty<double>());
                    var pontuacaoFiltros = CalcularPontuacaoFiltros(imovel, requisicao, maiorQuartosEntreCandidatos);
                    var scoreFinal = (PesoSemantico * similaridadeCoseno) + (PesoFiltros * pontuacaoFiltros);
                    return new ImovelScore(imovel, similaridadeCoseno, scoreFinal);
                })
                .Where(r => r.ScoreFinal > 0d)                 // retorna só relevantes
                .OrderByDescending(r => r.ScoreFinal)
                .ThenByDescending(r => r.Similaridade)
                .Take(requisicao.Limite)
                .ToList();

            return ranqueados;
        }

        private static double CalcularPontuacaoFiltros(ImovelModel imovel,BuscaImovelRequest requisicao,int maiorQuartosEntreCandidatos)
        {
            double soma = 0;
            int partes = 0;

            // Preço: quanto mais abaixo do teto, melhor (0..1)
            if (requisicao.PrecoMaximo is { } precoMaximo)
            {
                var proporcao = (double)((precoMaximo <= 0 || imovel.Preco >= precoMaximo)
                    ? 0
                    : (precoMaximo - imovel.Preco) / precoMaximo);

                soma += Math.Clamp(proporcao, 0, 1);
                partes++;
            }

            // Quartos: quanto acima do mínimo, melhor (0..1)
            if (requisicao.QuartosMinimos is { } quartosMinimos)
            {
                var denominador = Math.Max(2, maiorQuartosEntreCandidatos - quartosMinimos); // piso evita 0/0 e dá sensibilidade
                var proporcao = Math.Clamp((imovel.Quartos - quartosMinimos) / (double)denominador, 0, 1);

                soma += proporcao;
                partes++;
            }

            // Bairro: match exato = 1; caso contrário = 0
            if (!string.IsNullOrWhiteSpace(requisicao.Bairro))
            {
                soma += string.Equals(imovel.Bairro, requisicao.Bairro, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                partes++;
            }

            return partes > 0 ? soma / partes : 0;
        }
    }
}
