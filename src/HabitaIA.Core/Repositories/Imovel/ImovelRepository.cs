using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.Imovel.Model;
using HabitaIA.Core.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Core.Repositories.Imovel
{
    public class ImovelRepository : IImovelRepository
    {
        private readonly ContextoHabita _db;
        public ImovelRepository(ContextoHabita db) => _db = db;


        public async Task<List<(Guid Id, float[] Embedding)>> ListarEmbeddingsAsync(CancellationToken ct)
        {
            return await _db.Imoveis
                .AsNoTracking()
                .Select(i => new { i.Id, i.Embedding })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .Where(x => x.Embedding != null && x.Embedding.Length > 0)
                    .Select(x => (x.Id, x.Embedding))
                    .ToList(), ct);
        }

        public async Task<List<ImovelModel>> BuscarPorIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
        {
            var set = new HashSet<Guid>(ids);
            var lista = await _db.Imoveis.AsNoTracking()
                .Where(i => set.Contains(i.Id))
                .ToListAsync(ct);

            // reordena na mesma ordem de 'ids'
            var byId = lista.ToDictionary(i => i.Id);
            var ordered = new List<ImovelModel>(lista.Count);
            foreach (var id in ids)
                if (byId.TryGetValue(id, out var m)) ordered.Add(m);
            return ordered;
        }

        // (1) Compiled query para o hot path de candidatos
        private static readonly Func<ContextoHabita, decimal?, int?, string?, int, IAsyncEnumerable<ImovelModel>>
            _qryCandidatos = EF.CompileAsyncQuery(
                (ContextoHabita db, decimal? preco, int? quartos, string? bairro, int take) =>
                    db.Imoveis.AsNoTracking()
                      .Where(i => (preco == null || i.Preco <= preco)
                               && (quartos == null || i.Quartos == quartos)
                               && (bairro == null || i.Bairro == bairro))
                      .OrderByDescending(i => i.CreatedAt)
                      .Take(take)
            );

        public async Task<IReadOnlyList<ImovelModel>> BuscarCandidatosAsync(
            decimal? precoMax, int? quartosMin, string? bairro, int take, CancellationToken ct)
        {
            var list = new List<ImovelModel>(capacity: take);
            await foreach (var i in _qryCandidatos(_db, precoMax, quartosMin, bairro, take).WithCancellation(ct))
                list.Add(i);
            return list;
        }

        // (4) Pré-filtro lexical simples quando não há filtros estruturados
        public async Task<IReadOnlyList<ImovelModel>> BuscarCandidatosLexicalAsync(
            string consultaLivre, int take, CancellationToken ct)
        {
            var texto = (consultaLivre ?? "").ToLowerInvariant();

            // termos bem simples; ajuste conforme necessidade (tokenização melhor, stopwords, etc.)
            var termos = new List<string>();
            if (texto.Contains("cobertura")) termos.Add("cobertura");
            if (texto.Contains("apto") || texto.Contains("apartamento")) termos.Add("ap");
            if (texto.Contains("casa")) termos.Add("casa");
            if (texto.Contains("castelo")) termos.Add("castelo");
            if (texto.Contains("ouro preto") || texto.Contains("outro preto")) termos.Add("ouro preto");

            // se não extrair nada, volta top-N por data
            var q = _db.Imoveis.AsNoTracking();

            if (termos.Count > 0)
            {
                foreach (var t in termos)
                {
                    var like = $"%{t}%";
                    q = q.Where(i =>
                        EF.Functions.ILike(i.Titulo, like) ||
                        EF.Functions.ILike(i.Descricao, like) ||
                        EF.Functions.ILike(i.Bairro, like)
                    );
                }
            }

            return await q.OrderByDescending(i => i.CreatedAt)
                          .Take(take)
                          .ToListAsync(ct);
        }

        public async Task AdicionarAsync(ImovelModel model, CancellationToken ct)
        {
            _db.Set<ImovelModel>().Add(model);
            await _db.SaveChangesAsync(ct);
        }
        public async Task<IReadOnlyList<ImovelModel>> ListarTodosAsync(CancellationToken ct)
                    => await _db.Imoveis.AsNoTracking().OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        public async Task<IReadOnlyList<ImovelModel>> ListarPorTenantAsync(Guid tenantId, CancellationToken ct)
        {
            // O HasQueryFilter já restringe por TenantId; aqui só força a intenção.
            return await _db.Imoveis.AsNoTracking().Where(i => i.TenantId == tenantId)
                     .OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        }
    }
}
