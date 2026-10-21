# LLM Gateway

[![CI](https://github.com/GlexN3t/llm-gateway/actions/workflows/ci.yml/badge.svg)](https://github.com/GlexN3t/llm-gateway/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A self-hosted AI proxy that fronts multiple LLM providers behind one OpenAI-compatible
endpoint, with per-tenant API keys, cost tracking, PII redaction, and response caching.

Built with .NET 10 (ASP.NET Core minimal APIs), EF Core + SQLite, zero cloud dependencies.

## Features

| Area | What it does |
|---|---|
| Multi-provider routing | Route by model pattern (`gpt-*` → OpenAI, `claude-*` → Anthropic, `llama*`/`qwen*` → Ollama, `*` → fake provider for offline demos); exact matches beat prefixes, prefixes beat the wildcard |
| Tenant auth | Per-tenant API keys, PBKDF2-SHA256 hashed with per-key salt, plus a SHA-256 key index so a request is verified against one tenant instead of all of them; 401 on missing/invalid key |
| PII redaction | Best-effort detection of emails, IBANs, credit cards (Luhn) and phone numbers before data leaves the gateway; responses are re-hydrated for the caller |
| Cost tracking | Per-request token usage and USD cost from a configurable pricing table; `/v1/usage` summary per tenant |
| Caching | Exact-match response cache keyed by tenant + model + messages + params, TTL-based; `X-Cache: HIT/MISS` header; cache hits cost $0 |
| Resilience | Per-provider timeout, exponential backoff with jitter on 408/429/5xx, and upstream failures surfaced as RFC 7807 `problem+json` (502/503/504) instead of a stack trace |
| Rate limiting | Per-tenant fixed-window limiter (built into ASP.NET Core) with `429` + `Retry-After` |
| Observability | `X-Request-Id` correlation (inbound or generated), structured per-request logs with tenant, model, provider, cache state, tokens, cost and duration |
| Deployment | Hardened multi-stage container: chiseled runtime, non-root, no shell, read-only-friendly |

## Architecture

```
client ──► POST /v1/chat/completions
              │
              ├─ RequestIdMiddleware       (X-Request-Id correlation)
              ├─ ExceptionHandlingMiddleware(problem+json, no stack traces)
              ├─ ApiKeyAuthMiddleware      (key index → PBKDF2 verify → TenantContext)
              ├─ RateLimiter               (per-tenant fixed window → 429 + Retry-After)
              ├─ PII redaction             (outbound messages → tokens, map kept in memory)
              ├─ Cache lookup              (SHA-256 tenant+model+messages+params)
              ├─ ModelRouter               (model pattern → provider)
              ├─ Provider adapter          (OpenAI / Anthropic / Ollama / Fake)
              │     └─ timeout + backoff + error mapping
              ├─ Usage ledger              (tokens + cost → SQLite)
              └─ PII restore               (tokens → original values)
```

```
src/
  LlmGateway.Core/           domain: models, routing, providers, privacy, cost, caching contracts
  LlmGateway.Infrastructure/ EF Core SQLite persistence, tenant store, usage store, memory cache
  LlmGateway.Api/            minimal-API host, middleware, endpoints, config
tests/
  LlmGateway.Tests/          57 offline tests (fake provider + in-memory SQLite)
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

### `GET /v1/models`

OpenAI-style model list built from `Gateway:Models`, with the provider each model routes to.

### `GET /healthz` / `GET /readyz`

Liveness (`{"status":"ok"}`) and readiness (database reachable → `{"status":"ready"}`). Neither
requires an API key.

### Errors

Failures are returned as RFC 7807 `problem+json` with a `requestId` extension, never as a
stack trace:

| Situation | Status |
|---|---|
| Missing/invalid API key | `401` |
| Validation failure | `400` |
| Tenant exceeded its rate limit | `429` + `Retry-After` |
| Upstream rejected the request (4xx) | `502` |
| Upstream unreachable / 5xx | `503` |
| Upstream timeout | `504` |

## Configuration

All settings live in `appsettings.json` and can be overridden with `Section__Key` environment
variables (12-factor style).

| Key | Default | Notes |
|---|---|---|
| `Gateway:Routing` | ordered list of `{Pattern, Provider}` | Most specific pattern wins; `*` is the fallback |
| `Gateway:Models` | list of `{Id, Provider, DisplayName}` | Backs `GET /v1/models` |
| `Providers:OpenAI:BaseUrl` / `ApiKey` | `https://api.openai.com` / empty | Bearer auth when key set |
| `Providers:Anthropic:BaseUrl` / `ApiKey` | `https://api.anthropic.com` / empty | `x-api-key` header |
| `Providers:Ollama:BaseUrl` | `http://localhost:11434` | No auth by default |
| `Providers:<name>:TimeoutSeconds` | `60` (Ollama `120`) | Per-request upstream timeout |
| `Providers:<name>:MaxAttempts` | `3` (Ollama `2`) | Attempts for retryable upstream failures |
| `Storage:ConnectionString` | `Data Source=data/gateway.db` | SQLite file; directory auto-created |
| `Pricing:<model>` | see `appsettings.json`, `*` fallback | USD per 1M input/output tokens |
| `Caching:TtlSeconds` / `SizeLimit` | `300` / `1024` | In-memory cache TTL and max entries |
| `RateLimit:PermitPerMinute` / `QueueLimit` | `60` / `0` | Per-tenant fixed window |
| `Tenancy:DevApiKey` | auto-generated in logs (Development only) | Seed key for demo tenant |

Secrets belong in environment variables, never in `appsettings.json`.

## Testing

```bash
dotnet test
```

57 tests, fully offline: provider adapters, upstream error mapping and retry behaviour,
router specificity, auth/hashing and the key index, PII detection and round-trip restore,
cost math, usage store, cache key isolation, memory cache expiry.

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
- Each tenant also stores a SHA-256 *key index*, so an incoming key is verified against a
  single tenant instead of every tenant (the previous behaviour made each request cost
  `tenants × 210k` PBKDF2 iterations, which is a denial-of-service amplifier).
- Successful verifications are cached in memory for 60 seconds, so a key revocation (or a
  tenant setting change) takes up to 60 seconds to take effect. Failed verifications are
  cached for 5 seconds to blunt repeated guessing.
- Requests are rate limited per tenant (fixed window) and rejected with `429` + `Retry-After`.
- All usage and cache keys are tenant-scoped; cache keys are SHA-256 digests and never
  contain raw tenant PII (redacted text is hashed).
- PII detection is regex + validation based (Luhn for cards, mod-97 for IBANs). It is a
  best-effort guardrail, not a compliance boundary; test against your own data.
- No request bodies or prompts are logged, and upstream error bodies are never echoed back
  to callers (they can contain the prompt).

## Known limitations

- **No EF Core migrations yet** — the schema is created with `EnsureCreated()`. After a
  schema change the database file must be deleted (or migrated by hand). Adding
  `Microsoft.EntityFrameworkCore.Design` + the `dotnet-ef` tool is the first item on the
  roadmap.
- **No integration tests** — the HTTP pipeline (auth → rate limit → redaction → cache →
  usage) is covered by unit tests plus manual end-to-end runs, not by `WebApplicationFactory`
  tests. Adding `Microsoft.AspNetCore.Mvc.Testing` is also queued.
- Upstream error bodies are not surfaced, so provider-specific diagnostics are limited to
  status codes.
- PII detection is best-effort; streaming is not implemented; the cache is per-process.

## License

MIT — see [LICENSE](LICENSE).

## Roadmap

- EF Core migrations + `Microsoft.AspNetCore.Mvc.Testing` integration tests
- Streaming (SSE) responses
- Redis-backed cache for multi-instance deployments
- PostgreSQL storage option
- OpenTelemetry traces/metrics (designed, intentionally deferred)
- Tenant provisioning/admin API
