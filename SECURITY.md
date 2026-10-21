# Security Policy

## Supported versions

The `main` branch is the only supported target while the project is pre-1.0.

| Version | Supported |
| ------- | --------- |
| `main`  | ✅        |

## Reporting a vulnerability

Please report privately rather than opening a public issue or pull request.

Use GitHub's **Report a vulnerability** button on the Security tab of this repository
("Security" → "Report a vulnerability"), which opens a private advisory visible only to
the maintainer. Include:

- affected component and version/commit,
- steps to reproduce or a proof of concept,
- impact assessment (e.g. cross-tenant data access, key disclosure, PII leakage).

Expect an acknowledgement within a few days. Fixes ship as a private advisory, then a
patched release and a public changelog entry.

## Security model in one paragraph

The gateway is a multi-tenant proxy: API keys authenticate a tenant, and every usage
record and cache key is scoped to that tenant. Keys are stored as PBKDF2-SHA256 hashes
with per-key salts and never in plaintext. Outbound prompts are scanned for PII
(email, IBAN, card numbers, phone numbers) and redacted before leaving the gateway;
responses are re-hydrated for the caller only. PII detection is best-effort and
regex-based — it is a guardrail, not a compliance boundary.

## Known limitations

- PII detection can miss unusual formats and can in principle over-match; review
  before relying on it for regulated data.
- There is no rate limiting or quota enforcement per tenant yet.
- Provider adapters are exercised offline only; live upstream calls are not covered
  by the test suite.
