using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Interfaces
{
    public interface IEmbeddingService
    {
        // Agora retorna float[] já NORMALIZADO (||v||=1)
        Task<float[]> GenerateAsync(string text, CancellationToken ct);

        // (2) Cosine super-rápido com SIMD (vetores normalizados: cosine = dot)
        static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0d;

            int i = 0;
            var acc = Vector<float>.Zero;
            int step = Vector<float>.Count;

            // bloco vetorizado
            for (; i + step <= a.Length; i += step)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                acc += va * vb;
            }

            float dot = 0f;
            for (int k = 0; k < step; k++) dot += acc[k];

            // resto escalar
            for (; i < a.Length; i++) dot += a[i] * b[i];

            // como os vetores estão normalizados, dot == cosine
            return dot;
        }

        // Normaliza in-place (L2)
        static void NormalizeInPlace(Span<float> v)
        {
            double sum = 0;
            for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
            if (sum <= 0) return;
            float norm = (float)Math.Sqrt(sum);
            for (int i = 0; i < v.Length; i++) v[i] /= norm;
        }
    }
}
