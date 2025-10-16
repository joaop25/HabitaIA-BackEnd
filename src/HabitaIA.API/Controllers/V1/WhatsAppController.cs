using HabitaIA.API.DTOs.WhatsApp;
using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.NLP.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitaIA.API.Controllers.V1
{
    [ApiController]
    [Route("api/webhooks/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IImovelService _service;
        private readonly IFilterExtractionService _extractor;

        public WhatsAppController(IImovelService service, IFilterExtractionService extractor)
        {
            _service = service;
            _extractor = extractor;
        }

        [HttpPost("busca")]
        public async Task<IActionResult> Busca([FromBody] WhatsAppBuscaDTO payload, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(payload.message))
                return BadRequest("Mensagem vazia.");

            // 1) Extrai com Function Calling (Semantic Kernel)
            var f = await _extractor.ExtractAsync(payload.message, ct);

            // 2) Monta request interno (fallbacks)
            var req = new BuscaImovelRequest(
                ConsultaLivre: payload.message,
                PrecoMaximo: f.PrecoMaximo,
                QuartosMinimos: f.QuartosMinimos,
                Bairro: f.Bairro,
                Limite: f.Limite ?? 5
            );

            // 3) Busca e formata resposta
            var resultados = await _service.BuscarAsync(req, ct);
            if (resultados.Count == 0)
                return Ok(new { text = "Não encontrei imóveis com esse perfil. Quer ajustar preço, bairro ou quartos?" });

            var linhas = new List<string> { "✨ *Resultados mais relevantes:*" };
            foreach (var r in resultados)
            {
                var i = r.Imovel;
                linhas.Add(
    $@"• *{i.Titulo}* — {i.Bairro}, {i.Cidade}-{i.UF}
  Quartos: {i.Quartos} | Banheiros: {i.Banheiros} | Área: {i.Area:N0} m²
  Preço: R$ {i.Preco:N0}
  Relevância: {(r.ScoreFinal * 100):N1}%");
            }
            return Ok(new { text = string.Join("\n\n", linhas) });
        }
    }
}
