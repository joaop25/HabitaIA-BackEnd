using HabitaIA.Business.Imovel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Interfaces
{
    public interface IImovelRepository
    {
        Task AdicionarAsync(ImovelModel model, CancellationToken ct);

        Task<IReadOnlyList<ImovelModel>> BuscarCandidatosAsync(
            decimal? precoMax, int? quartosMin, string? bairro, int take, CancellationToken ct);

        // NOVO: só Id+Embedding (já filtrado por tenant via HasQueryFilter)
        Task<List<(Guid Id, float[] Embedding)>> ListarEmbeddingsAsync(CancellationToken ct);

        // NOVO: buscar detalhes pelos ids (preserva ordenação do chamador)
        Task<List<ImovelModel>> BuscarPorIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

        Task<IReadOnlyList<ImovelModel>> ListarPorTenantAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<ImovelModel>> ListarTodosAsync(CancellationToken ct);
    }
}
