# Utcp.AspNetCore

[![CI](https://github.com/khalidabuhakmeh/utcp-aspnetcore/actions/workflows/ci.yml/badge.svg)](https://github.com/khalidabuhakmeh/utcp-aspnetcore/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Utcp.AspNetCore.svg)](https://www.nuget.org/packages/Utcp.AspNetCore)

Utcp.AspNetCore publishes a [UTCP](https://www.utcp.io/) (Universal Tool Calling Protocol) v1.1 manual at `GET /utcp`. It builds the manual automatically from your app's ASP.NET Core OpenAPI document, so AI agents can discover and call your HTTP endpoints directly.

## Install

```sh
dotnet add package Utcp.AspNetCore
```

## Quickstart

Then:

```csharp
using Utcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();   // required: Microsoft.AspNetCore.OpenApi
builder.Services.AddUtcp();      // optionally: AddUtcp(o => o.DocumentName = "v2")

var app = builder.Build();

app.MapUtcp("/utcp");            // returns an IEndpointConventionBuilder; chain .AllowAnonymous() etc.

app.Run();
```

Set an `operationId` on each endpoint (for example `.WithName("GetWeather")`). The `/utcp` endpoint is excluded from the OpenAPI document.

## Options (`UtcpOptions`)

| Option | Default | Description |
| --- | --- | --- |
| `DocumentName` | `"v1"` | Name of the OpenAPI document to convert. |
| `ManualVersion` | `"1.0.0"` | Value of `manual_version` in the manual. |

## OpenAPI → UTCP mapping

| OpenAPI | UTCP |
| --- | --- |
| `operationId` | tool `name` (operations without one are skipped) |
| summary / description | tool `description` |
| operation tags | tool `tags` |
| path / query / header parameters | `inputs.properties` (`required` honored; path params always required) |
| JSON request body | `inputs.properties.body`, with `tool_call_template.body_field = "body"` and `content_type` set to the media type |
| header parameters | `tool_call_template.header_fields` |
| `200` response (else first `2xx`) JSON schema | `outputs` |
| path + base URL | `tool_call_template.url`, keeping `{param}` placeholders; `call_template_type` is `http` |
| `http` / `bearer` security scheme (operation-level, else document-level; first match wins) | `auth`: `{ "auth_type": "api_key", "api_key": "Bearer ${<SCHEMEID>_TOKEN}", "var_name": "Authorization", "location": "header" }` |
| `apiKey` security scheme | `auth`: `{ "auth_type": "api_key", "api_key": "${<SCHEMEID>_API_KEY}", "var_name": "<scheme Name>", "location": "<header|query|cookie>" }` |
| `http` / `basic` security scheme | `auth`: `{ "auth_type": "basic", "username": "${<SCHEMEID>_USERNAME}", "password": "${<SCHEMEID>_PASSWORD}" }` |

`<SCHEMEID>` is the security scheme's component id, uppercased with non-alphanumerics replaced by `_` (id `Bearer` → `BEARER_TOKEN`).

`$ref` component schemas are inlined so each tool is self-contained.

## Run the sample

```bash
dotnet run --project samples/Utcp.Sample.Api
curl -s http://localhost:5080/utcp
```

The sample exposes six endpoints:

- `GET /weather/{city}?days=3` (`GetWeather`): path and query inputs.
- `POST /orders` (`CreateOrder`): JSON body.
- `GET /me` (`GetCurrentUser`): requires JWT bearer auth, so its tool includes the `auth` block.
- `GET /admin/stats` (`GetAdminStats`): API key in the `X-Api-Key` header (scheme `ApiKeyHeader`).
- `GET /reports` (`GetReports`): API key in the `api_key` query parameter (scheme `ApiKeyQuery`).
- `GET /legacy/export` (`GetLegacyExport`): HTTP Basic (scheme `Basic`).

**Demo credentials** (sample only, bound from the `DemoAuth` configuration section): `appsettings.json` ships **empty values and the handlers fail closed** — secured endpoints reject everything until credentials are configured. `appsettings.Development.json` supplies dev-only values (API key `demo-key`; basic `demo` / `demo`), so they only work when `ASPNETCORE_ENVIRONMENT=Development` (the default for `dotnet run`). For anything else, supply your own via user-secrets or environment variables (e.g. `DemoAuth__ApiKey`). Example: `curl -H "X-Api-Key: demo-key" http://localhost:5080/admin/stats`.

> ⚠️ The query-string API key (`GetReports`) exists to demonstrate UTCP's `location: "query"` mapping. Query strings commonly end up in access logs and proxies — prefer header-based keys in real APIs.

## Tests

```bash
dotnet test
```

The tests call the live `/utcp` endpoint and validate it against a JSON Schema (`tests/Utcp.AspNetCore.Tests/Schemas/utcp-manual.schema.json`).

## Security notes

- `/utcp` is anonymous by default and enumerates all documented operations, including secured ones. That is by design (it is a discovery endpoint), but if your API surface is sensitive, protect it: `app.MapUtcp("/utcp").RequireAuthorization();`.
- The manual never contains real tokens — secured tools emit the placeholder `Bearer ${BEARER_TOKEN}` for the agent to substitute.
- Tool URLs use the OpenAPI document's `servers[0].Url` when it is absolute; otherwise they reflect the incoming request's `Host` header. Behind proxies or on the open internet, add a server entry to your OpenAPI document (e.g. via `AddOpenApi` + a document transformer that sets `document.Servers`) so an attacker-controlled Host header can't be reflected into the manual.
- The sample's `AddJwtBearer()` has no authority/key configuration and is **not production-ready**; it exists only to demonstrate the security-scheme → UTCP auth mapping.

## Limitations

- HTTP call templates only.
- Only `http` bearer/basic and `apiKey` security schemes are mapped to auth. OAuth2 and other schemes are ignored.
- Operations need an `operationId`, or they are skipped.
- Targets .NET 9 and OpenAPI 3.0 documents generated by `Microsoft.AspNetCore.OpenApi`.

## Code style

Format and clean up with:

```bash
jb cleanupcode utcp-library.sln --profile="Utcp Cleanup"
```

Settings live in `.editorconfig` and the team-shared `utcp-library.sln.DotSettings` (which defines the "Utcp Cleanup" profile and makes it the default).

## Releasing

Push a bare version tag like `0.2.0` (prereleases like `0.2.0-preview.1` also work). The release workflow builds, tests, packs with the version taken from the tag, pushes to NuGet, and creates a GitHub release with the `.nupkg` attached.

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (GitHub OIDC via the `NuGet/login` action) — no long-lived API key. One-time setup:

1. On nuget.org → account → **Trusted Publishing**, add a policy for repository `khalidabuhakmeh/utcp-aspnetcore`, workflow file `release.yml`.
2. Set the `NUGET_USER` repository secret to the nuget.org username that owns the policy.
