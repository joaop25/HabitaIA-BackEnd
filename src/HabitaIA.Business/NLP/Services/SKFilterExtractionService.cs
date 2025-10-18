using HabitaIA.Business.NLP.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HabitaIA.Business.NLP.Services
{
    public class SKFilterExtractionService : IFilterExtractionService
    {
        private readonly IChatCompletionService _chat;
        private readonly Kernel _kernel;

        public SKFilterExtractionService(IChatCompletionService chat, ExtractFiltersPlugin plugin)
        {
            _chat = chat;

            var kb = Kernel.CreateBuilder();
            kb.Services.AddSingleton(_chat);
            _kernel = kb.Build();

            // expõe a função [KernelFunction] ao modelo
            _kernel.Plugins.AddFromObject(plugin, "filters");
        }

        public async Task<ExtractedFilters> ExtractAsync(string userText, CancellationToken ct)
        {
            var history = new ChatHistory();
            history.AddSystemMessage("""
Você extrai filtros para busca de imóveis (pt-BR).
Retorne APENAS uma chamada à função "extract_filters" com:
- precoMaximo: number|null (teto em BRL; "X mil"/"Xk"=X*1000; "entre A e B"→B)
- quartosMinimos: int|null (mínimo)
- bairro: string|null (somente se citado literalmente; marcos como "UFMG" não viram bairro)
- limite: int|null (se o usuário pedir N resultados)
Se faltar valor → null. Não invente.
Ex.: "até 300 mil, 2 quartos no Castelo" → extract_filters({ "precoMaximo":300000,"quartosMinimos":2,"bairro":"Castelo","limite":null })
""");

            history.AddUserMessage(userText);

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
                // isto habilita o Function Calling com auto-invocação das KernelFunctions
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // roda o chat; se o modelo chamar a função, o SK a invoca
            var response = await _chat.GetChatMessageContentAsync(history, settings, _kernel, ct);

            // Opcional: mantenha o histórico sincronizado
            history.Add(response);

            // === PEGAR O RESULTADO DA FUNÇÃO ===
            // Em SK atuais, procure por FunctionResultContent dentro de msg.Items
            // (fallback: tentar interpretar msg.Content como JSON)
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];

                // 1) Caminho moderno: msg.Items (lista heterogênea)
                if (msg.Items is { Count: > 0 })
                {
                    foreach (var item in msg.Items)
                    {
                        if (item is FunctionResultContent funcResult)
                        {
                            var raw = funcResult.Result as string;
                            var parsed = TryParseFiltersJson(raw);
                            if (parsed is not null) return parsed;
                        }
                    }
                }

                // 2) Fallback: algumas versões devolvem JSON em msg.Content (string)
                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    var parsed = TryParseFiltersJson(msg.Content);
                    if (parsed is not null) return parsed;
                }
            }

            // Se o modelo não chamou a função (ou não veio JSON), volte tudo nulo
            return new ExtractedFilters(null, null, null, null);
        }

        private static ExtractedFilters? TryParseFiltersJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                decimal? preco =
                    root.TryGetProperty("precoMaximo", out var p1) && p1.ValueKind == JsonValueKind.Number ? p1.GetDecimal() :
                    root.TryGetProperty("PrecoMaximo", out var p2) && p2.ValueKind == JsonValueKind.Number ? p2.GetDecimal() : null;

                int? quartos =
                    root.TryGetProperty("quartosMinimos", out var q1) && q1.ValueKind == JsonValueKind.Number ? q1.GetInt32() :
                    root.TryGetProperty("QuartosMinimos", out var q2) && q2.ValueKind == JsonValueKind.Number ? q2.GetInt32() : null;

                string? bairro =
                    root.TryGetProperty("bairro", out var b1) && b1.ValueKind == JsonValueKind.String ? b1.GetString() :
                    root.TryGetProperty("Bairro", out var b2) && b2.ValueKind == JsonValueKind.String ? b2.GetString() : null;

                int? limite =
                    root.TryGetProperty("limite", out var l1) && l1.ValueKind == JsonValueKind.Number ? l1.GetInt32() :
                    root.TryGetProperty("Limite", out var l2) && l2.ValueKind == JsonValueKind.Number ? l2.GetInt32() : (int?)null;

                bairro = string.IsNullOrWhiteSpace(bairro) ? null
                    : string.Join(' ', bairro.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));
                limite = limite is int n ? Math.Clamp(n, 1, 100) : null;

                return new ExtractedFilters(preco, quartos, bairro, limite);
            }
            catch
            {
                return null;
            }
        }

    }
}
