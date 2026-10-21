# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Multi-provider routing (OpenAI, Anthropic, Ollama) with a built-in offline fake provider
- Per-tenant API key authentication (PBKDF2-SHA256, per-key salt) with a SHA-256 key index
- PII redaction for emails, IBANs, card numbers and phone numbers, with response re-hydration
- SQLite usage ledger with per-request token counts and USD cost tracking (`/v1/usage`)
- Tenant-scoped exact-match response cache with TTL and `X-Cache: HIT|MISS`
- OpenAI-compatible `POST /v1/chat/completions`, `GET /v1/models`, `GET /healthz`, `GET /readyz`
- Per-provider request timeout and exponential backoff with jitter on retryable upstream failures
- Upstream failures mapped to RFC 7807 `problem+json` (502/503/504) with a `requestId` extension
- Per-tenant fixed-window rate limiting with `429` + `Retry-After`
- `X-Request-Id` correlation and structured per-request logs (tenant, model, provider, cache, tokens, cost, duration)
- Hardened multi-stage container definition (chiseled runtime, non-root, no shell)
- CI: build, test, published-artifact smoke test, container build, CodeQL analysis, Dependabot
- Signed commits, MIT license, contributing guide, security policy, issue/PR templates
- `global.json` SDK pinning, `Directory.Build.props` with warnings-as-errors

### Fixed
- **Routing never applied the configured patterns.** JSON configuration providers enumerate
  child keys alphabetically, so the `*` fallback was always matched first and every model was
  routed to the fake provider. Routing is now an ordered list, and the router ranks exact
  matches above prefixes above the wildcard regardless of configuration order.
- **API key verification was O(tenants) PBKDF2 iterations per request**, which made a wrong
  key cost seconds of CPU. Tenants now carry a SHA-256 key index so only the matching tenant
  is verified, and successful verifications are cached for 60 seconds.
- Guarded empty upstream `choices` arrays (previously an unhandled index error).

### Known limitations
- No EF Core migrations yet: the schema is created with `EnsureCreated()`, so a schema change
  requires deleting the database file
- No integration tests (`Microsoft.AspNetCore.Mvc.Testing` is not referenced yet)
- No streaming (SSE) responses yet
- In-memory cache only — not shared across instances
- PII detection is regex-based and best-effort
- Provider adapters are not exercised against live upstreams in CI
