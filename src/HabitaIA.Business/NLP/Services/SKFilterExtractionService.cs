using HabitaIA.Business.NLP.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HabitaIA.Business.NLP.Services
{

    public class SKFilterExtractionService : IFilterExtractionService
    {
        private readonly IChatCompletionService _chat;
        private readonly Kernel _kernel;

        // Limita paralelismo para evitar filas/429 no provedor
        private static readonly SemaphoreSlim _rateGate = new(initialCount: 4);

        // Opções para aceitar números em string e ignorar maiúsc./minúsc. nas chaves
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        // DTO interno só para desserializar a resposta da função
        private sealed class FiltersPayload
        {
            public decimal? PrecoMaximo { get; set; }
            public int? QuartosMinimos { get; set; }
            public string? Bairro { get; set; }
            public int? Limite { get; set; }
        }


        // Retry leve para 429/503 com backoff exponencial + jitter
        private static readonly AsyncRetryPolicy _retry429 = Policy
     .Handle<HttpOperationException>(ex =>
         ex.StatusCode.HasValue &&
         (ex.StatusCode.Value == HttpStatusCode.TooManyRequests ||
          ex.StatusCode.Value == HttpStatusCode.ServiceUnavailable))
     .WaitAndRetryAsync(
         retryCount: 4,
         sleepDurationProvider: attempt =>
         {
             var baseMs = (int)Math.Min(200 * Math.Pow(2, attempt), 2000);
             return TimeSpan.FromMilliseconds(baseMs + Random.Shared.Next(50, 250));
         });

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
            // 1) Prompt mínimo (menos tokens = menor latência)
            var history = new ChatHistory();
            history.AddSystemMessage("""
Extraia filtros (pt-BR) e responda APENAS chamando "extract_filters" com:
- precoMaximo:number|null  ("X mil"/"Xk"=X*1000; "entre A e B"→B)
- quartosMinimos:int|null  (número citado; backend tratará como EXATO)
- bairro:string|null       (somente se citado literalmente)
- limite:int|null          (se o usuário pedir N resultados)
Se faltar, use null. Não invente.
""");
            history.AddUserMessage(userText);

            // 2) Config enxuta: sem AutoInvoke, saída curta
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
                MaxTokens = 64,
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions
            };

            // 3) Timeout curto para o extrator (degrade rápido)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            await _rateGate.WaitAsync(cts.Token);
            try
            {
                // 4) ÚNICA chamada ao modelo (sem rodadas extras)
                var msg = await _retry429.ExecuteAsync(tok =>
                    _chat.GetChatMessageContentAsync(history, settings, _kernel, tok),
                    cts.Token);

                // 5) Ler diretamente a FunctionCall retornada
                if (msg.Items is { Count: > 0 })
                {
                    foreach (var item in msg.Items)
                    {
                        if (item is FunctionCallContent call)
                        {
                            // Nome da função chamada pelo modelo
                            var funcName = call.FunctionName; // <- em SK modernos é FunctionName

                            if (string.Equals(funcName, "extract_filters", StringComparison.OrdinalIgnoreCase))
                            {
                                // Arguments é KernelArguments => converta para JSON
                                string? raw = null;
                                if (call.Arguments is not null)
                                {
                                    // KernelArguments implementa IEnumerable<KeyValuePair<string, object?>>
                                    var dict = call.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
                                    raw = JsonSerializer.Serialize(dict);
                                }

                                var parsed = TryParseFiltersJson(raw);
                                if (parsed is not null) return parsed;
                            }
                        }
                    }
                }


                // 6) Fallback: algumas versões retornam JSON em Content
                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    var parsed = TryParseFiltersJson(msg.Content);
                    if (parsed is not null) return parsed;
                }

                // 7) Degradação elegante: sem filtros quando não houver tool-call
                return new ExtractedFilters(null, null, null, null);
            }
            catch (OperationCanceledException)
            {
                // Timeout → segue sem filtros explícitos
                return new ExtractedFilters(null, null, null, null);
            }
            finally
            {
                _rateGate.Release();
            }
        }

        private static ExtractedFilters? TryParseFiltersJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var dto = JsonSerializer.Deserialize<FiltersPayload>(json, _jsonOpts);
                if (dto is null) return null;

                // Normalizações leves
                string? bairroNorm = string.IsNullOrWhiteSpace(dto.Bairro) ? null
                    : string.Join(' ', dto.Bairro.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));

                int? limiteClamp = dto.Limite is int n ? Math.Clamp(n, 1, 100) : null;

                return new ExtractedFilters(dto.PrecoMaximo, dto.QuartosMinimos, bairroNorm, limiteClamp);
            }
            catch
            {
                return null;
            }
        }


    }
}
