using HabitaIA.Business.NLP.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HabitaIA.Business.NLP.Services
{
    public sealed class OpenAIFilterExtractionService : IFilterExtractionService
    {
        private readonly HttpClient _http;
        private readonly string _model;

        public OpenAIFilterExtractionService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                    cfg["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

            // modelo barato e bom para FC
            _model = cfg["OpenAI:ChatModel"] ?? "gpt-4o-mini";
        }

        public async Task<ExtractedFilters> ExtractAsync(string userText, CancellationToken ct)
        {
            // Definição da tool (function) com JSON Schema
            var body = new
            {
                model = _model,
                temperature = 0,
                messages = new object[]
                {
                new { role = "system", content =
                    "Você é um extrator de filtros para busca de imóveis. " +
                    "Extraia precoMaximo (decimal em BRL), quartosMinimos (int) e bairro (string) de textos em PT-BR. " +
                    "Se não tiver informação explícita, retorne null. Não invente." },
                new { role = "user", content = userText }
                },
                tools = new object[]
                {
                new {
                    type = "function",
                    function = new {
                        name = "extract_filters",
                        description = "Extrai filtros da mensagem do usuário para busca de imóveis.",
                        parameters = new {
                            type = "object",
                            properties = new Dictionary<string,object> {
                                ["precoMaximo"] = new {
                                    description = "Preço máximo em reais. Ex.: 300000 para R$ 300.000,00.",
                                    type = "number",
                                    minimum = 0
                                },
                                ["quartosMinimos"] = new {
                                    description = "Quantidade mínima de quartos.",
                                    type = "integer",
                                    minimum = 0
                                },
                                ["bairro"] = new {
                                    description = "Nome do bairro (apenas o bairro, sem cidade/UF).",
                                    type = "string",
                                    minLength = 2
                                },
                                ["limite"] = new {
                                    description = "Quantidade de resultados desejada (Top-N).",
                                    type = "integer",
                                    minimum = 1,
                                    maximum = 100
                                }
                            },
                            required = new string[] {}, // nada é obrigatório
                            additionalProperties = false
                        }
                    }
                }
                },
                tool_choice = "auto"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // Caminhos JSON (chat completions): choices[0].message.tool_calls[0].function.arguments
            var root = doc.RootElement;
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                return new ExtractedFilters(null, null, null, null);

            var msg = choices[0].GetProperty("message");

            // Se o modelo não chamou a função, retorna tudo null
            if (!msg.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.GetArrayLength() == 0)
                return new ExtractedFilters(null, null, null, null);

            var argsStr = toolCalls[0].GetProperty("function").GetProperty("arguments").GetString() ?? "{}";

            // arguments é um JSON; parseamos com tolerância
            using var argsDoc = JsonDocument.Parse(argsStr);
            var a = argsDoc.RootElement;

            decimal? precoMax = a.TryGetProperty("precoMaximo", out var p) && p.ValueKind is JsonValueKind.Number
                ? p.GetDecimal()
                : null;

            int? quartosMin = a.TryGetProperty("quartosMinimos", out var q) && q.ValueKind is JsonValueKind.Number
                ? q.GetInt32()
                : null;

            string? bairro = a.TryGetProperty("bairro", out var b) && b.ValueKind is JsonValueKind.String
                ? NormalizeBairro(b.GetString())
                : null;

            int? limite = a.TryGetProperty("limite", out var l) && l.ValueKind is JsonValueKind.Number
                ? Math.Clamp(l.GetInt32(), 1, 100)
                : null;

            return new ExtractedFilters(precoMax, quartosMin, string.IsNullOrWhiteSpace(bairro) ? null : bairro, limite);
        }

        private static string? NormalizeBairro(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts.Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));
        }
    }
}
