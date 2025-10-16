using HabitaIA.Business.Imovel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Interfaces
{
    public record BuscaImovelRequest(
    string ConsultaLivre,
    decimal? PrecoMaximo,
    int? QuartosMinimos,
    string? Bairro,
    int Limite = 20
);

    public record ImovelScore(ImovelModel Imovel, double Similaridade, double ScoreFinal);

    public interface IImovelService
    {
        Task<IReadOnlyList<ImovelScore>> BuscarAsync(BuscaImovelRequest req, CancellationToken ct);
    }
}
