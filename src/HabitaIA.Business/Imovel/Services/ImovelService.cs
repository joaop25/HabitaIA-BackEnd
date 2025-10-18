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

        private const double PesoSemantico = 0.75;
        private const double PesoFiltros = 0.25;

        // cortes mínimos “seguros”
        private const double MinCosineHard = 0.06; // nunca retorne abaixo disso
        private const double MinFinalScore = 0.00; // você já pediu > 0

        public ImovelService(IImovelRepository repo, IEmbeddingService emb)
        {
            _repo = repo; _emb = emb;
        }

        public async Task<IReadOnlyList<ImovelScore>> BuscarAsync(BuscaImovelRequest req, CancellationToken ct)
        {
            // 1) Normaliza limite
            var limiteNormalizado = req.Limite <= 0 ? 5 : Math.Min(req.Limite, 100);

            // 2) Embedding da consulta (normalizado pelo IEmbeddingService)
            var consultaEmbedding = await _emb.GenerateAsync(req.ConsultaLivre, ct);

            // 3) Há filtros estruturados?
            var temFiltrosEstruturados =
                (req.PrecoMaximo is not null) ||
                (!string.IsNullOrWhiteSpace(req.Bairro)) ||
                // OBS: quartos é EXATO no repositório; aqui só detectamos se veio informado
                (req.QuartosMinimos is not null);

            if (!temFiltrosEstruturados)
            {
                // =======================
                // MODO SÓ TEXTO: EMBEDDING PURO
                // =======================
                var todosVetores = await _repo.ListarEmbeddingsAsync(ct); // (Id, Embedding) já filtrado por tenant
                if (todosVetores.Count == 0) return Array.Empty<ImovelScore>();

                // Calcula coseno para todos (SIMD no IEmbeddingService.Cosine)
                var cosPorId = new List<(Guid Id, double Cos)>(todosVetores.Count);
                foreach (var (id, emb) in todosVetores)
                {
                    var cos = IEmbeddingService.Cosine(consultaEmbedding, emb);
                    cosPorId.Add((id, cos));
                }

                // Corte dinâmico (percentil “alto”) + corte mínimo absoluto
                var cosOrdenadosDesc = cosPorId.Select(s => s.Cos).OrderByDescending(x => x).ToArray();
                var idxP80 = (int)Math.Floor(cosOrdenadosDesc.Length * 0.2); // top 20% como referência
                var p80 = cosOrdenadosDesc.Length > 0 ? cosOrdenadosDesc[Math.Clamp(idxP80, 0, cosOrdenadosDesc.Length - 1)] : 0.0;
                var minCos = Math.Max(MinCosineHard, p80);

                // Top-N por coseno acima do corte
                var topIds = cosPorId
                    .Where(s => s.Cos >= minCos)
                    .OrderByDescending(s => s.Cos)
                    .Select(s => s.Id)
                    .Distinct()                // evita dup
                    .Take(limiteNormalizado)
                    .ToList();

                if (topIds.Count == 0) return Array.Empty<ImovelScore>();

                var itens = await _repo.BuscarPorIdsAsync(topIds, ct);

                // Mapeia cosenos por Id para O(1)
                var cosLookup = cosPorId.ToDictionary(x => x.Id, x => x.Cos);

                var ranqueados = itens
                    .DistinctBy(i => i.Id)
                    .Select(i =>
                    {
                        var cos = cosLookup.TryGetValue(i.Id, out var v) ? v : 0d;
                        return new ImovelScore(i, cos, cos); // score final = somente embedding
                    })
                    .OrderByDescending(r => r.ScoreFinal)
                    .ThenByDescending(r => r.Similaridade)
                    .ToList();

                return ranqueados;
            }
            else
            {
                // ===================================
                // MODO FILTROS + EMBEDDING (sem quartos no score)
                // quartos são EXATOS no repositório (i.Quartos == req.QuartosMinimos)
                // ===================================
                var tamanhoAmostra = Math.Max(limiteNormalizado * 5, 50);

                var amostra = await _repo.BuscarCandidatosAsync(
                    req.PrecoMaximo, req.QuartosMinimos, req.Bairro, take: tamanhoAmostra, ct);

                if (amostra.Count == 0) return Array.Empty<ImovelScore>();

                // pontuação só de PREÇO e BAIRRO (sem quartos)
                double CalcularPontuacaoFiltros(ImovelModel imovel)
                {
                    double soma = 0;
                    int partes = 0;

                    if (req.PrecoMaximo is { } precoMax)
                    {
                        var proporcao = (double)((precoMax <= 0 || imovel.Preco >= precoMax)
                            ? 0
                            : (precoMax - imovel.Preco) / precoMax);
                        soma += Math.Clamp(proporcao, 0, 1);
                        partes++;
                    }

                    if (!string.IsNullOrWhiteSpace(req.Bairro))
                    {
                        soma += string.Equals(imovel.Bairro, req.Bairro, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        partes++;
                    }

                    return partes > 0 ? soma / partes : 0;
                }

                const double wSem = PesoSemantico;
                const double wFil = PesoFiltros;

                var ranqueados = amostra
                    .DistinctBy(i => i.Id)
                    .Select(im =>
                    {
                        var cos = IEmbeddingService.Cosine(consultaEmbedding, im.Embedding ?? Array.Empty<float>());
                        var f = CalcularPontuacaoFiltros(im);
                        var final = (wSem * cos) + (wFil * f);
                        return new ImovelScore(im, cos, final);
                    })
                    .Where(r => r.Similaridade >= MinCosineHard && r.ScoreFinal > MinFinalScore)
                    .OrderByDescending(r => r.ScoreFinal)
                    .ThenByDescending(r => r.Similaridade)
                    .Take(limiteNormalizado)
                    .ToList();

                return ranqueados;
            }
        }

    }

}
