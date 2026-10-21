using System.Net.Http.Headers;
using LlmGateway.Api.Endpoints;
using LlmGateway.Api.Middleware;
using LlmGateway.Core;
using LlmGateway.Core.Caching;
using LlmGateway.Core.Privacy;
using LlmGateway.Core.Providers;
using LlmGateway.Core.Routing;
using LlmGateway.Core.Security;
using LlmGateway.Core.Tenancy;
using LlmGateway.Core.Usage;
using LlmGateway.Infrastructure.Caching;
using LlmGateway.Infrastructure.Persistence;
using LlmGateway.Infrastructure.Tenancy;
using LlmGateway.Infrastructure.Usage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var providersConfig = builder.Configuration.GetSection("Providers");

builder.Services.AddSingleton<ApiKeyHasher>();
builder.Services.AddSingleton<TenantAuthenticator>();
builder.Services.AddSingleton<PiiRedactor>();
builder.Services.AddScoped<TenantContext>();

var cacheTtl = TimeSpan.FromSeconds(builder.Configuration.GetValue("Caching:TtlSeconds", 300));
builder.Services.AddMemoryCache(options =>
    options.SizeLimit = builder.Configuration.GetValue("Caching:SizeLimit", 1024));
builder.Services.AddSingleton<IChatCache>(sp =>
    new MemoryChatCache(sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), cacheTtl));

var storageConnectionString = builder.Configuration["Storage:ConnectionString"] ?? "Data Source=data/gateway.db";

builder.Services.AddDbContextFactory<GatewayDbContext>(options => options.UseSqlite(storageConnectionString));

builder.Services.AddSingleton<ITenantStore, DbTenantStore>();
builder.Services.AddSingleton<IUsageStore, EfUsageStore>();
builder.Services.AddSingleton(new CostCalculator(
    builder.Configuration.GetSection("Pricing").Get<Dictionary<string, ModelPrice>>() ?? []));

builder.Services.AddHttpClient("openai", http =>
{
    http.BaseAddress = new Uri(providersConfig["OpenAI:BaseUrl"] ?? "https://api.openai.com");
    var apiKey = providersConfig["OpenAI:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
});

builder.Services.AddHttpClient("anthropic", http =>
{
    http.BaseAddress = new Uri(providersConfig["Anthropic:BaseUrl"] ?? "https://api.anthropic.com");
    var apiKey = providersConfig["Anthropic:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
    }
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
});

builder.Services.AddHttpClient("ollama", http =>
{
    http.BaseAddress = new Uri(providersConfig["Ollama:BaseUrl"] ?? "http://localhost:11434");
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
});

builder.Services.AddKeyedSingleton<ILlmProvider>("fake", (_, _) => new FakeProvider());
builder.Services.AddKeyedSingleton<ILlmProvider>("openai", (sp, _) =>
    new OpenAiProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai")));
builder.Services.AddKeyedSingleton<ILlmProvider>("anthropic", (sp, _) =>
    new AnthropicProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("anthropic")));
builder.Services.AddKeyedSingleton<ILlmProvider>("ollama", (sp, _) =>
    new OllamaProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama")));

builder.Services.AddSingleton(sp =>
{
    var routing = builder.Configuration.GetSection("Gateway:Routing").Get<Dictionary<string, string>>() ?? [];
    var rules = routing.Select(kv => (kv.Key, sp.GetRequiredKeyedService<ILlmProvider>(kv.Value)));
    return new ModelRouter(rules);
});

var devApiKey = builder.Configuration["Tenancy:DevApiKey"];
if (string.IsNullOrWhiteSpace(devApiKey))
{
    devApiKey = InMemoryTenantStore.GenerateKey();
}

var devTenantId = builder.Configuration["Tenancy:DevTenantId"] ?? "dev";
var devTenantName = builder.Configuration["Tenancy:DevTenantName"] ?? "Development Tenant";

var app = builder.Build();

EnsureSqliteDirectory(storageConnectionString);

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GatewayDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    if (app.Environment.IsDevelopment() && !await db.Tenants.AnyAsync(t => t.Id == devTenantId))
    {
        var hasher = scope.ServiceProvider.GetRequiredService<ApiKeyHasher>();
        var (hash, salt) = hasher.Hash(devApiKey);
        db.Tenants.Add(new TenantEntity
        {
            Id = devTenantId,
            Name = devTenantName,
            ApiKeyHash = hash,
            ApiKeySalt = salt,
        });
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Logger.LogInformation("Dev tenant '{TenantId}' ready. API key: {DevApiKey}", devTenantId, devApiKey);
}
else
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapChatEndpoints();
app.MapUsageEndpoints();

app.Run();

static void EnsureSqliteDirectory(string connectionString)
{
    var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
    if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
    {
        return;
    }

    var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }
}
