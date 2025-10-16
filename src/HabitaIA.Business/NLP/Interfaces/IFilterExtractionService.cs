using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.NLP.Interfaces
{
    public sealed record ExtractedFilters(
     decimal? PrecoMaximo,
     int? QuartosMinimos,
     string? Bairro,
     int? Limite
 );

    public interface IFilterExtractionService
    {
        Task<ExtractedFilters> ExtractAsync(string userText, CancellationToken ct);
    }
}
