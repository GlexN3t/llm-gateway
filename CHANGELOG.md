# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Multi-provider routing (OpenAI, Anthropic, Ollama) with a built-in offline fake provider
- Per-tenant API key authentication (PBKDF2-SHA256, per-key salt)
- PII redaction for emails, IBANs, card numbers and phone numbers, with response re-hydration
- SQLite usage ledger with per-request token counts and USD cost tracking (`/v1/usage`)
- Tenant-scoped exact-match response cache with TTL and `X-Cache: HIT|MISS`
- OpenAI-compatible `POST /v1/chat/completions` and `GET /healthz`
- Hardened multi-stage container definition (chiseled runtime, non-root, no shell)
- CI: build, test, published-artifact smoke test, container build, CodeQL analysis, Dependabot
- Signed commits, MIT license, contributing guide, security policy, issue/PR templates

### Known limitations
- No streaming (SSE) responses yet
- In-memory cache only — not shared across instances
- PII detection is regex-based and best-effort
- Provider adapters are not exercised against live upstreams in CI
