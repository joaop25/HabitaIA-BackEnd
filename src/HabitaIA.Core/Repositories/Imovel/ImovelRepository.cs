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

        public async Task AdicionarAsync(ImovelModel model, CancellationToken ct)
        {
            _db.Set<ImovelModel>().Add(model);
            await _db.SaveChangesAsync(ct);
        }
        public async Task<IReadOnlyList<ImovelModel>> ListarTodosAsync(CancellationToken ct)
      => await _db.Imoveis.AsNoTracking().OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        // Aplica APENAS filtros tradicionais no banco (eficiente),
        // devolve um conjunto candidato já com Embedding carregado.
        public async Task<IReadOnlyList<ImovelModel>> BuscarCandidatosAsync(decimal? precoMax, int? quartosMin, string? bairro, int take, CancellationToken ct)
        {
            var q = _db.Imoveis.AsNoTracking();

            if (precoMax is { } p) q = q.Where(i => i.Preco <= p);
            if (quartosMin is { } qmin) q = q.Where(i => i.Quartos >= qmin);
            if (!string.IsNullOrWhiteSpace(bairro)) q = q.Where(i => i.Bairro == bairro);

            // Limita a amostra para rankear por embedding em memória
            return await q.OrderByDescending(i => i.CreatedAt)
                          .Take(take)
                          .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ImovelModel>> ListarPorTenantAsync(Guid tenantId, CancellationToken ct)
        {
            // O HasQueryFilter já restringe por TenantId; aqui só força a intenção.
            return await _db.Imoveis.AsNoTracking().Where(i => i.TenantId == tenantId)
                     .OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        }
    }
}
