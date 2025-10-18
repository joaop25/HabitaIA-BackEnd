using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Model
{
    public class ImovelModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }         // Imobiliária
        public string Titulo { get; set; } = default!;
        public string Descricao { get; set; } = default!;
        public string Bairro { get; set; } = default!;
        public string Cidade { get; set; } = default!;
        public string UF { get; set; } = "MG";
        public int Quartos { get; set; }
        public int Banheiros { get; set; }
        public decimal Preco { get; set; }
        public double Area { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Embedding salvo como array de double (sem pgvector!)
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
