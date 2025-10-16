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

        Task<IReadOnlyList<ImovelModel>> ListarPorTenantAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<ImovelModel>> ListarTodosAsync(CancellationToken ct);
    }
}
