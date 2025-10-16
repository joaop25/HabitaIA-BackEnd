using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Interfaces
{
    public interface IEmbeddingService
    {
        Task<double[]> GenerateAsync(string text, CancellationToken ct);

        static double Cosine(double[] a, double[] b)
        {
            if (a is null || b is null || a.Length == 0 || b.Length == 0) return 0;
            if (a.Length != b.Length) return 0; // segurança: dimensões devem bater
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}
