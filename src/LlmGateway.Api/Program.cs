using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using LlmGateway.Api;
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

var rateLimitPermit = builder.Configuration.GetValue("RateLimit:PermitPerMinute", 60);
var rateLimitQueue = builder.Configuration.GetValue("RateLimit:QueueLimit", 0);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Rate limit exceeded",
                detail: "Too many requests for this tenant. Retry once the window resets.")
            .ExecuteAsync(context.HttpContext);
    };

    options.AddPolicy(RateLimiting.PolicyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Items[HttpContextItems.TenantId] as string
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitPermit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = rateLimitQueue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));
});

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

ProviderTransportOptions ProviderOptions(string provider) => new(
    MaxAttempts: builder.Configuration.GetValue($"Providers:{provider}:MaxAttempts", 3),
    Timeout: TimeSpan.FromSeconds(builder.Configuration.GetValue($"Providers:{provider}:TimeoutSeconds", 60)));

builder.Services.AddKeyedSingleton<ILlmProvider>("fake", (_, _) => new FakeProvider());
builder.Services.AddKeyedSingleton<ILlmProvider>("openai", (sp, _) =>
    new OpenAiProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai"), ProviderOptions("openai")));
builder.Services.AddKeyedSingleton<ILlmProvider>("anthropic", (sp, _) =>
    new AnthropicProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("anthropic"), ProviderOptions("anthropic")));
builder.Services.AddKeyedSingleton<ILlmProvider>("ollama", (sp, _) =>
    new OllamaProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama"), ProviderOptions("ollama")));

builder.Services.AddSingleton(sp =>
{
    var routing = builder.Configuration.GetSection("Gateway:Routing").Get<GatewayRoute[]>() ?? [];
    var rules = routing.Select(kv => (kv.Pattern, sp.GetRequiredKeyedService<ILlmProvider>(kv.Provider)));
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
            ApiKeyLookup = ApiKeyHasher.ComputeLookup(devApiKey),
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

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/readyz", async (IDbContextFactory<GatewayDbContext> dbFactory, CancellationToken ct) =>
{
    try
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unavailable", error = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapModelEndpoints();
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
