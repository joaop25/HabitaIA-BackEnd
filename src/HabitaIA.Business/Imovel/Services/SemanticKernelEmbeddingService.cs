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

        public SemanticKernelEmbeddingService(ITextEmbeddingGenerationService embedder)
        {
            _embedder = embedder;
        }

        public async Task<float[]> GenerateAsync(string text, CancellationToken ct)
        {
            // SK retorna ReadOnlyMemory<float>
            var mem = await _embedder.GenerateEmbeddingAsync(text,null,ct);
            var vec = mem.ToArray(); // float[]

            // (2) normaliza antes de persistir/usar
            IEmbeddingService.NormalizeInPlace(vec.AsSpan());

            return vec;
        }
    }
}
