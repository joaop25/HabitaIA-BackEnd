using HabitaIA.Business.Imovel.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Business.Imovel.Services
{
    public class SemanticKernelEmbeddingService : IEmbeddingService
    {
        private readonly ITextEmbeddingGenerationService _embedder;
        public SemanticKernelEmbeddingService(ITextEmbeddingGenerationService embedder) => _embedder = embedder;

        public async Task<double[]> GenerateAsync(string text, CancellationToken ct)
        {
            var mem = await _embedder.GenerateEmbeddingAsync(text,null, ct); // ReadOnlyMemory<float>
            var floats = mem.ToArray();                 // float[]
            return Array.ConvertAll(floats, x => (double)x);
        }
    }
}
