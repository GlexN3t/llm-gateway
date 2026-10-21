# Contributing

Thanks for taking the time to contribute.

## Getting set up

The project targets **.NET 10**.

```bash
git clone git@github.com:GlexN3t/llm-gateway.git
cd llm-gateway
dotnet restore
dotnet build
dotnet test
```

No secrets are needed: the `fake` provider and an offline test suite cover the whole
request path, so you never need a real OpenAI/Anthropic key to work on the code.

## Ground rules

- **Tests are part of the change.** New behaviour needs a test; bug fixes need a test
  that fails before the fix.
- **No secrets.** Never commit API keys, tokens, `.env` files, or captured request
  bodies containing real PII. Use `deploy/.env.example` as the template.
- **Keep the layers honest.** `Core` holds domain logic and has no dependency on
  ASP.NET or EF Core; `Infrastructure` owns persistence; `Api` owns HTTP concerns.
- **Public responses are OpenAI-compatible.** Don't change the wire shape without
  saying so in the PR description.
- **Small commits, clear messages.** Conventional-ish prefixes (`feat:`, `fix:`, `docs:`,
  `refactor:`, `chore:`, `test:`) help the changelog.

## Running the full check locally

```bash
dotnet build -c Release
dotnet test -c Release
```

CI additionally publishes the API and smoke-tests `/healthz` plus a chat completion,
and builds the hardened container image.

## Reporting security issues

Please don't open a public issue for security problems — see [SECURITY.md](SECURITY.md).
