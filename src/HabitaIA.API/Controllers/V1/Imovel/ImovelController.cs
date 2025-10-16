using HabitaIA.API.DTOs.Imovel;
using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.Imovel.Model;
using Microsoft.AspNetCore.Mvc;

namespace HabitaIA.API.Controllers.V1.Imovel
{
    [ApiController]
    [Route("api/imoveis")]
    public class ImovelController : ControllerBase
    {
        private readonly IImovelService _service;
        private readonly IImovelRepository _repo;
        private readonly IEmbeddingService _embedding;

        public ImovelController(IImovelService service, IImovelRepository repo, IEmbeddingService embedding)
        {
            _service = service; _repo = repo; _embedding = embedding;
        }

        [HttpPost("busca")]
        public async Task<ActionResult<IEnumerable<ImovelRespostaDTO>>> Buscar([FromBody] BuscaImovelDTO dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.consulta))
                return BadRequest("Campo 'consulta' é obrigatório.");

            var req = new BuscaImovelRequest(dto.consulta, dto.precoMaximo, dto.quartosMinimos, dto.bairro, dto.limite);
            var resultados = await _service.BuscarAsync(req, ct);

            var payload = resultados.Select(r => new ImovelRespostaDTO(
                r.Imovel.Id, r.Imovel.Titulo, r.Imovel.Bairro, r.Imovel.Cidade, r.Imovel.UF,
                r.Imovel.Quartos, r.Imovel.Banheiros, r.Imovel.Preco, r.Imovel.Area,
                Math.Round(r.Similaridade, 6), Math.Round(r.ScoreFinal, 6)
            ));

            return Ok(payload);
        }

        [HttpPost]
        public async Task<ActionResult> Criar([FromBody] CriarImovelDTO dto, CancellationToken ct)
        {
            // gera embedding a partir de texto rico do imóvel
            var texto = $"{dto.titulo}. {dto.descricao}. Bairro {dto.bairro}, {dto.cidade}-{dto.uf}. " +
                        $"{dto.quartos} quartos, {dto.banheiros} banheiros, {dto.area} m2. Preço {dto.preco}.";
            var emb = await _embedding.GenerateAsync(texto, ct);

            var model = new ImovelModel
            {
                Id = Guid.NewGuid(),
                TenantId = dto.tenantId,
                Titulo = dto.titulo,
                Descricao = dto.descricao,
                Bairro = dto.bairro,
                Cidade = dto.cidade,
                UF = dto.uf,
                Quartos = dto.quartos,
                Banheiros = dto.banheiros,
                Area = dto.area,
                Preco = dto.preco,
                CreatedAt = DateTime.UtcNow,
                Embedding = emb
            };

            await _repo.AdicionarAsync(model, ct);
            return CreatedAtAction(nameof(Buscar), new { id = model.Id }, new { model.Id });
        }
    }
}
