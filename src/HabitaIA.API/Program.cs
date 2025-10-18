using dotenv.net;
using System.Net.Http;

using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.Imovel.Services;
using HabitaIA.Business.NLP;
using HabitaIA.Business.NLP.Interfaces;
using HabitaIA.Business.NLP.Services;
using HabitaIA.Core.Context;
using HabitaIA.Core.Repositories.Imovel;

using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

using Npgsql.EntityFrameworkCore.PostgreSQL; // EnableRetryOnFailure

// 1) Carrega .env antes de montar o builder
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// 2) Configuração
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var cfg = builder.Configuration;

// Helpers locais
string? GetOpenAIKey() =>
    (cfg["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"))?.Trim();

string? GetProjectId() => cfg["OpenAI:Project"]?.Trim();

string GetModel(string key, string fallback) =>
    (cfg[key]?.Trim()).NullIfEmpty() ?? fallback;

static HttpClient BuildHttpClientWithProject(string? projectId)
{
    var http = new HttpClient();
    if (!string.IsNullOrWhiteSpace(projectId))
        http.DefaultRequestHeaders.Add("OpenAI-Project", projectId);
    return http;
}

// 3) EF Core + PostgreSQL (sem pooling, pois seu DbContext aceita IHttpContextAccessor)
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ContextoHabita>(opt =>
    opt.UseNpgsql(
        cfg.GetConnectionString("pg"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: new[] { "40001" }
        )
    )
);

// 4) Domínio
builder.Services.AddScoped<IImovelRepository, ImovelRepository>();
builder.Services.AddScoped<IImovelService, ImovelService>();

// 5) OpenAI / Semantic Kernel

// 5.1 Chat para Function Calling (SK)
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var apiKey = GetOpenAIKey();
    var modelId = GetModel("OpenAI:ChatModel", "gpt-4o-mini");
    var project = GetProjectId();

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("OpenAI:ApiKey não configurada (verifique .env ou variável de ambiente).");

    var http = BuildHttpClientWithProject(project);
    return new OpenAIChatCompletionService(modelId, apiKey, httpClient: http);
});

// 5.2 Embeddings (SK)
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
{
    var apiKey = GetOpenAIKey();
    var modelId = GetModel("OpenAI:EmbeddingModel", "text-embedding-3-small");
    var project = GetProjectId();

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("OpenAI:ApiKey não configurada (verifique .env ou variável de ambiente).");

    var http = BuildHttpClientWithProject(project);
    return new OpenAITextEmbeddingGenerationService(modelId, apiKey, httpClient: http);
});

// 5.3 Plugin + Service para Function Calling (extração de filtros)
builder.Services.AddSingleton<ExtractFiltersPlugin>();
builder.Services.AddScoped<IFilterExtractionService, SKFilterExtractionService>();

// 5.4 Serviço de IA de domínio (gera embeddings normalizados float[])
builder.Services.AddScoped<IEmbeddingService, SemanticKernelEmbeddingService>();

// 6) Web / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Se usar autenticação:
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static class StringExt
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
