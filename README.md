# LLM Gateway

A self-hosted AI proxy that fronts multiple LLM providers behind one OpenAI-compatible
endpoint, with per-tenant API keys, cost tracking, PII redaction, and response caching.

Built with .NET 10 (ASP.NET Core minimal APIs), EF Core + SQLite, zero cloud dependencies.

## Features

| Area | What it does |
|---|---|
| Multi-provider routing | Route by model prefix (`gpt-*` → OpenAI, `claude-*` → Anthropic, `llama*`/`qwen*` → Ollama, `*` → fake provider for offline demos) |
| Tenant auth | Per-tenant API keys, PBKDF2-SHA256 hashed with per-key salt; 401 on missing/invalid key |
| PII redaction | Best-effort detection of emails, IBANs, credit cards (Luhn) and phone numbers before data leaves the gateway; responses are re-hydrated for the caller |
| Cost tracking | Per-request token usage and USD cost from a configurable pricing table; `/v1/usage` summary per tenant |
| Caching | Exact-match response cache keyed by tenant + model + messages + params, TTL-based; `X-Cache: HIT/MISS` header; cache hits cost $0 |
| Deployment | Hardened multi-stage container: chiseled runtime, non-root, no shell, read-only-friendly |

## Architecture

```
client ──► POST /v1/chat/completions
              │
              ├─ ApiKeyAuthMiddleware      (PBKDF2 verify → TenantContext)
              ├─ PII redaction             (outbound messages → tokens, map kept in memory)
              ├─ Cache lookup              (SHA-256 tenant+model+messages+params)
              ├─ ModelRouter               (model pattern → provider)
              ├─ Provider adapter          (OpenAI / Anthropic / Ollama / Fake)
              ├─ Usage ledger              (tokens + cost → SQLite)
              └─ PII restore               (tokens → original values)
```

```
src/
  LlmGateway.Core/           domain: models, routing, providers, privacy, cost, caching contracts
  LlmGateway.Infrastructure/ EF Core SQLite persistence, tenant store, usage store, memory cache
  LlmGateway.Api/            minimal-API host, middleware, endpoints, config
tests/
  LlmGateway.Tests/          45 offline tests (fake provider + in-memory SQLite)
deploy/
  Containerfile              hardened multi-stage build
  .env.example               secrets template (placeholders only)
```

## Quick start (development)

Requires .NET 10 SDK.

```bash
cd src/LlmGateway.Api
dotnet run
```

In Development the gateway seeds a tenant and logs its API key. Set one explicitly:

```bash
Tenancy__DevApiKey=gw-dev-demo-key-change-me dotnet run
```

Then:

```bash
curl -s http://localhost:5210/v1/chat/completions \
  -H "Authorization: Bearer gw-dev-demo-key-change-me" \
  -H "Content-Type: application/json" \
  -d '{"model":"demo-model","messages":[{"role":"user","content":"hello"}]}'
```

The `demo-model` route uses the built-in fake provider, so it works offline.

## API

### `POST /v1/chat/completions`

OpenAI-compatible request/response shape. Response includes the gateway `provider` field
and `X-Cache: HIT|MISS`.

```json
{
  "id": "fake-…",
  "object": "chat.completion",
  "created": 1792542694,
  "model": "demo-model",
  "provider": "fake",
  "choices": [{ "index": 0, "message": { "role": "assistant", "content": "…" }, "finishReason": "stop" }],
  "usage": { "promptTokens": 2, "completionTokens": 9, "totalTokens": 11 }
}
```

### `GET /v1/usage`

Per-tenant summary: request count, tokens, total USD, cached request count, recent records.

### `GET /healthz`

Liveness probe: `{"status":"ok"}`.

## Configuration

All settings live in `appsettings.json` and can be overridden with `Section__Key` environment
variables (12-factor style).

| Key | Default | Notes |
|---|---|---|
| `Gateway:Routing` | `gpt-*`→openai, `claude-*`→anthropic, `llama*`/`qwen*`→ollama, `*`→fake | First matching pattern wins |
| `Providers:OpenAI:BaseUrl` / `ApiKey` | `https://api.openai.com` / empty | Bearer auth when key set |
| `Providers:Anthropic:BaseUrl` / `ApiKey` | `https://api.anthropic.com` / empty | `x-api-key` header |
| `Providers:Ollama:BaseUrl` | `http://localhost:11434` | No auth by default |
| `Storage:ConnectionString` | `Data Source=data/gateway.db` | SQLite file; directory auto-created |
| `Pricing:<model>` | see `appsettings.json`, `*` fallback | USD per 1M input/output tokens |
| `Caching:TtlSeconds` / `SizeLimit` | `300` / `1024` | In-memory cache TTL and max entries |
| `Tenancy:DevApiKey` | auto-generated in logs (Development only) | Seed key for demo tenant |

Secrets belong in environment variables, never in `appsettings.json`.

## Testing

```bash
dotnet test
```

45 tests, fully offline: provider adapters, router, auth/hashing, PII detection and
round-trip restore, cost math, usage store, cache key isolation, memory cache expiry.

## Deployment (Podman)

```bash
podman build -f deploy/Containerfile -t llm-gateway .

podman run --name llm-gateway -p 8080:8080 \
  --env-file deploy/.env \
  -v llm-gateway-data:/data \
  llm-gateway
```

- Runtime image is `aspnet:10.0-noble-chiseled`: non-root UID 1654, no shell, no package manager.
- `/data` is owned by the app user and holds the SQLite database; mount a volume to persist it.
- The app listens on `:8080` in the container.
- Dev demo tenant is only seeded when `ASPNETCORE_ENVIRONMENT=Development`; for a quick
  container demo add `-e ASPNETCORE_ENVIRONMENT=Development -e Tenancy__DevApiKey=…`.
- In production, provision tenants explicitly (admin/provisioning tooling is on the roadmap)
  and probe `/healthz` from your orchestrator — the chiseled image has no curl for
  container-level healthchecks.

## Security notes

- API keys are stored as PBKDF2-SHA256 hashes with per-key salts; raw keys never persist.
- All usage and cache keys are tenant-scoped; cache keys are SHA-256 digests and never
  contain raw tenant PII (redacted text is hashed).
- PII detection is regex + validation based (Luhn for cards, mod-97 for IBANs). It is a
  best-effort guardrail, not a compliance boundary; test against your own data.
- No request bodies or prompts are logged.

## Roadmap

- Streaming (SSE) responses
- Redis-backed cache for multi-instance deployments
- PostgreSQL storage option
- OpenTelemetry traces/metrics (designed, intentionally deferred)
- Tenant provisioning/admin API
