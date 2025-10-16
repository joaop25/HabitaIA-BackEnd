using HabitaIA.Business.Imovel.Interfaces;
using HabitaIA.Business.Imovel.Services;
using HabitaIA.Business.NLP;
using HabitaIA.Business.NLP.Interfaces;
using HabitaIA.Business.NLP.Services;
using HabitaIA.Core.Context;
using HabitaIA.Core.Repositories.Imovel;

using Microsoft.EntityFrameworkCore;

// Semantic Kernel (Chat + Embeddings)
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- EF Core + PostgreSQL ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ContextoHabita>(opt =>
    opt.UseNpgsql(cfg.GetConnectionString("pg"))
);

// ---------- Domínio ----------
builder.Services.AddScoped<IImovelRepository, ImovelRepository>();
builder.Services.AddScoped<IImovelService, ImovelService>();

// ---------- OpenAI / Semantic Kernel ----------
// 1) Chat para Function Calling (SK)
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var c = sp.GetRequiredService<IConfiguration>();
    var apiKey = c["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    return new OpenAIChatCompletionService(
        modelId: c["OpenAI:ChatModel"] ?? "gpt-4o-mini",
        apiKey: apiKey
    );
});

// 2) Embeddings (SK) — ***ESSENCIAL*** para o seu SemanticKernelEmbeddingService
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
{
    var c = sp.GetRequiredService<IConfiguration>();
    var apiKey = c["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    return new OpenAITextEmbeddingGenerationService(
        modelId: "text-embedding-3-small", // ou "text-embedding-3-large"
        apiKey: apiKey
    );
});

// 3) Plugin + Service para Function Calling (extração de filtros)
builder.Services.AddSingleton<ExtractFiltersPlugin>();
builder.Services.AddScoped<IFilterExtractionService, SKFilterExtractionService>();

// 4) Serviço de IA de domínio que usa ITextEmbeddingGenerationService
builder.Services.AddScoped<IEmbeddingService, SemanticKernelEmbeddingService>();

// ---------- Web ----------
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

// Se for usar [Authorize], lembre de adicionar app.UseAuthentication() antes de UseAuthorization()
app.UseAuthorization();

app.MapControllers();
app.Run();
