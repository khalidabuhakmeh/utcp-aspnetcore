# UTCP ASP.NET Core Library

## TL;DR
Build `Utcp.AspNetCore`, a .NET 9 library that converts the app's generated OpenAPI document into a UTCP v1.1 manual served at `GET /utcp`, plus a sample minimal API and an xUnit test suite that validates the live manual against an authored JSON Schema.

## Context
- Repo root: `/Users/khalidabuhakmeh/RiderProjects/utcp-library` (empty, not a git repo, macOS).
- .NET 9 SDK; ASP.NET Core built-in OpenAPI (`Microsoft.AspNetCore.OpenApi` 9.x, which uses `Microsoft.OpenApi` 1.6.x — `OpenApiDocument`, `OpenApiOperation`, `OperationType`, `ParameterLocation`, `SecuritySchemeType`).
- Obtaining the document in .NET 9: `Microsoft.AspNetCore.OpenApi` registers the internal `OpenApiDocumentService` keyed by document name and exposes the public `Microsoft.Extensions.ApiDescriptions.IDocumentProvider` (`GenerateAsync(string documentName, TextWriter writer)`). Recommended approach: resolve `IDocumentProvider` from DI, generate JSON into a `StringWriter`, then parse with `Microsoft.OpenApi.Readers.OpenApiStringReader` (add `Microsoft.OpenApi.Readers` 1.6.x package). This avoids internal types and works regardless of transformers. Fallback: a document transformer (`IOpenApiDocumentTransformer`) that caches the last document — only populated after `/openapi/v1.json` is hit, so do NOT rely on it alone.
- UTCP manual shape (snake_case):
  ```json
  { "manual_version": "1.0.0", "utcp_version": "1.0.1",
    "tools": [{ "name": "...", "description": "...", "tags": [],
      "inputs": { "type": "object", "properties": {}, "required": [] },
      "outputs": { ... },
      "tool_call_template": { "name": "...", "call_template_type": "http",
        "url": "https://host/weather/{city}", "http_method": "GET",
        "content_type": "application/json", "body_field": "body",
        "header_fields": ["X-Foo"],
        "auth": { "auth_type": "api_key", "api_key": "Bearer ${API_TOKEN_VARNAME}", "var_name": "Authorization", "location": "header" } } }] }
  ```
  Placement decision: `auth` lives inside `tool_call_template` (per UTCP v1 spec, auth is a call-template property). Assumption recorded below.

## Scope
- In scope: class library, sample app, tests, JSON Schema for UTCP manual, solution, README, .gitignore, git init + commit.
- Out of scope: NuGet packaging/publishing, non-HTTP call templates, OAuth2/apiKey-scheme mapping (only `http`+`bearer`), `$ref` deep inlining beyond component schema resolution, OpenAPI 3.1 / .NET 10.
- Constraints / assumptions:
  - Target `net9.0` everywhere; nullable + implicit usings enabled.
  - `auth` placed inside `tool_call_template`; token var name is configurable via options (default `API_TOKEN_VARNAME` literal per request).
  - `$ref` schemas are resolved inline from `components.schemas` (one level recursive, cycle-guarded) so tool inputs/outputs are self-contained.
  - `outputs` uses the `200` response's `application/json` schema (fallback first 2xx; else `{}` ).
  - Base URL: `UtcpOptions.BaseUrl` if set, otherwise `{Request.Scheme}://{Request.Host}{Request.PathBase}`.

## Objectives
- `services.AddUtcp(o => ...)` + `app.MapUtcp("/utcp")` publish a valid UTCP manual.
- Sample app demonstrates params, body, and bearer-secured endpoint.
- Tests validate schema conformance and converter rules.

## Dependencies and Order
1. Scaffold solution/projects (everything depends on it).
2. Library models → converter → DI/endpoint extensions (converter needs models; extensions need converter).
3. Sample app (needs library API).
4. JSON Schema file, then tests (tests need sample + schema).
5. README, then git init/commit last so everything is included.

## Tasks

- [x] 1. Scaffold solution and projects
  - **What**: Create solution, three projects, references, packages, .gitignore.
  - **Files**: `utcp-library.sln`, `src/Utcp.AspNetCore/Utcp.AspNetCore.csproj`, `samples/Utcp.Sample.Api/Utcp.Sample.Api.csproj`, `tests/Utcp.AspNetCore.Tests/Utcp.AspNetCore.Tests.csproj`, `.gitignore`, `global.json` (optional, pin 9.0.x with rollForward latestFeature)
  - **Depends on**: None
  - **Implementation outline**:
    1. `dotnet new sln -n utcp-library`; `dotnet new classlib -o src/Utcp.AspNetCore -f net9.0`; `dotnet new web -o samples/Utcp.Sample.Api -f net9.0`; `dotnet new xunit -o tests/Utcp.AspNetCore.Tests -f net9.0`; add all to sln.
    2. Library csproj: `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, packages `Microsoft.AspNetCore.OpenApi` 9.0.*, `Microsoft.OpenApi.Readers` 1.6.*.
    3. Sample: reference library; packages `Microsoft.AspNetCore.OpenApi` 9.0.*, `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.*.
    4. Tests: reference sample + library; packages `Microsoft.AspNetCore.Mvc.Testing` 9.0.*, `JsonSchema.Net` (latest 7.x).
    5. `dotnet new gitignore`. Delete template `Class1.cs`/`UnitTest1.cs`.
  - **Pitfalls / non-goals**:
    - Keep `Microsoft.OpenApi.Readers` major version aligned with what `Microsoft.AspNetCore.OpenApi` 9 pulls (1.6.x) to avoid type conflicts.
  - **Acceptance**:
    - `dotnet build` succeeds with 0 errors.

- [x] 2. UTCP manual model types
  - **What**: Serializable POCOs for the manual.
  - **Files**: `src/Utcp.AspNetCore/Models/UtcpManual.cs`, `Models/UtcpTool.cs`, `Models/HttpCallTemplate.cs`, `Models/ApiKeyAuth.cs`, `src/Utcp.AspNetCore/UtcpJson.cs`
  - **Depends on**: 1
  - **Implementation outline**:
    1. `UtcpManual { ManualVersion="1.0.0", UtcpVersion="1.0.1", List<UtcpTool> Tools }`.
    2. `UtcpTool { Name, Description, List<string> Tags, JsonObject Inputs, JsonObject Outputs, HttpCallTemplate ToolCallTemplate }`.
    3. `HttpCallTemplate { Name, CallTemplateType="http", Url, HttpMethod, ContentType="application/json", BodyField?, List<string>? HeaderFields, ApiKeyAuth? Auth }`.
    4. `ApiKeyAuth { AuthType="api_key", ApiKey, VarName, Location="header" }`.
    5. `UtcpJson.SerializerOptions`: `JsonNamingPolicy.SnakeCaseLower`, `DefaultIgnoreCondition = WhenWritingNull`, indented.
  - **Pitfalls / non-goals**:
    - Use `System.Text.Json.Nodes.JsonObject` for schemas; snake_case policy does not rename JsonObject keys (good — schema keys must stay as-is).
  - **Acceptance**:
    - Builds; serializing a sample manual yields `manual_version`, `tool_call_template`, `call_template_type`, no null `auth`.

- [x] 3. OpenAPI → UTCP converter
  - **What**: Pure converter from `OpenApiDocument` to `UtcpManual`.
  - **Files**: `src/Utcp.AspNetCore/OpenApiToUtcpConverter.cs`, `src/Utcp.AspNetCore/OpenApiSchemaConverter.cs`, `src/Utcp.AspNetCore/UtcpOptions.cs`
  - **Depends on**: 2
  - **Implementation outline**:
    1. `UtcpOptions { string DocumentName="v1"; string? BaseUrl; string ManualVersion="1.0.0"; string TokenVariableName="API_TOKEN_VARNAME"; }`.
    2. `Convert(OpenApiDocument doc, string baseUrl, UtcpOptions o)`: iterate `doc.Paths` × `path.Operations`; keep only Get/Post/Put/Delete/Patch; skip null/empty `OperationId`.
    3. url = `baseUrl.TrimEnd('/') + pathKey` (keeps `{param}` placeholders).
    4. inputs: `type: object`, `properties` from path/query/header parameters (schema converted, `description` copied), `required` for required params (path always required). Request body: pick `application/json` content (else first); add property `body` with converted schema; `required` if `RequestBody.Required`; set `body_field="body"`, `content_type` = chosen media type.
    5. header params → `header_fields` list (null if none).
    6. outputs: response "200" → else first key starting with "2" → json schema; else `{"type":"object"}`.
    7. description = Summary ?? Description ?? "". tags = operation tag names.
    8. Security: effective requirements = `operation.Security` if non-empty else `doc.SecurityRequirements`; if any referenced scheme has `Type==Http` and `Scheme` equals "bearer" (case-insensitive) → `ApiKeyAuth { ApiKey=$"Bearer ${{{o.TokenVariableName}}}", VarName="Authorization" }`.
    9. `OpenApiSchemaConverter.ToJsonObject(OpenApiSchema, doc)`: serialize via `schema.SerializeAsV3(OpenApiJsonWriter)` into string → `JsonNode.Parse`, then recursively replace `{"$ref":"#/components/schemas/X"}` with resolved component JSON (track visited set to stop cycles, leaving `$ref` on cycle). Simpler alt: build from properties manually — prefer serialize approach.
  - **Pitfalls / non-goals**:
    - Microsoft.OpenApi `Reference` objects: when serializing a schema that has `Reference` set, writer emits `$ref`; resolve against `doc.Components.Schemas[Reference.Id]` serialized with `SerializeAsV3WithoutReference`.
    - Security scheme in requirement keys is an `OpenApiSecurityScheme` with `Reference`; look up the real scheme in `doc.Components.SecuritySchemes` by `Reference.Id`.
    - Don't emit empty `required` array (omit when empty).
  - **Acceptance**:
    - Builds; converter has no ASP.NET HTTP dependencies (unit-testable).

- [x] 4. DI and endpoint extensions
  - **What**: `AddUtcp` and `MapUtcp`.
  - **Files**: `src/Utcp.AspNetCore/UtcpServiceCollectionExtensions.cs`, `src/Utcp.AspNetCore/UtcpEndpointRouteBuilderExtensions.cs`, `src/Utcp.AspNetCore/UtcpManualProvider.cs`
  - **Depends on**: 3
  - **Implementation outline**:
    1. `AddUtcp(this IServiceCollection, Action<UtcpOptions>? configure = null)`: `services.Configure(configure)`, register singleton `UtcpManualProvider`.
    2. `UtcpManualProvider.GetManualAsync(HttpContext)`: resolve `IDocumentProvider` (namespace `Microsoft.Extensions.ApiDescriptions`) from `RequestServices`; `GenerateAsync(options.DocumentName, stringWriter)`; parse with `new OpenApiStringReader().Read(json, out var diag)`; compute base URL; call converter. Cache the parsed document (lazy, thread-safe) — base URL is per-request so convert per request.
    3. `MapUtcp(this IEndpointRouteBuilder, string pattern = "/utcp")`: `MapGet(pattern, async ctx => Results.Json(manual, UtcpJson.SerializerOptions))`, `.ExcludeFromDescription()`, return `RouteHandlerBuilder`/`IEndpointConventionBuilder` so callers can chain `.AllowAnonymous()`.
    4. If `IDocumentProvider` missing, throw `InvalidOperationException("Call services.AddOpenApi() ...")`.
  - **Pitfalls / non-goals**:
    - `IDocumentProvider` is public interface in `Microsoft.AspNetCore.OpenApi` 9 (in `Microsoft.Extensions.ApiDescriptions` namespace); if not resolvable as public, fall back to the document-transformer capture + internally invoking `/openapi/{name}.json` is NOT acceptable — instead register a transformer via `services.Configure<OpenApiOptions>(name, o => o.AddDocumentTransformer(...))` and trigger generation through `IDocumentProvider`. Verify at implementation time.
    - `/utcp` must be excluded from the OpenAPI doc so it doesn't appear as a tool.
  - **Acceptance**:
    - `dotnet build` passes; public API surface is exactly `AddUtcp`, `MapUtcp`, `UtcpOptions`, models.

- [x] 5. Sample API
  - **What**: Minimal API demonstrating the library.
  - **Files**: `samples/Utcp.Sample.Api/Program.cs`, `samples/Utcp.Sample.Api/BearerSecuritySchemeTransformer.cs`, `samples/Utcp.Sample.Api/appsettings.json`, `samples/Utcp.Sample.Api/Properties/launchSettings.json`, `samples/Utcp.Sample.Api/Utcp.Sample.Api.http`
  - **Depends on**: 4
  - **Implementation outline**:
    1. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`; `AddAuthorization()`.
    2. `AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>())` — transformer adds `components.securitySchemes["Bearer"] = { type: http, scheme: bearer, bearerFormat: JWT }` and adds a security requirement only to operations whose endpoint metadata has `IAuthorizeData` (use `IOpenApiOperationTransformer` via `context.Description.ActionDescriptor.EndpointMetadata`, or a document transformer iterating `IApiDescriptionGroupCollectionProvider`). Prefer an operation transformer for per-op requirement + document transformer for the scheme.
    3. `AddUtcp()`.
    4. Endpoints: `GET /weather/{city}?days=int` `.WithName("GetWeather").WithSummary(...).WithTags("Weather")` returning `WeatherForecast[]` record; `POST /orders` body `CreateOrderRequest(string Product, int Quantity)` returns `Order` `.WithName("CreateOrder").WithTags("Orders")`; `GET /me` `.WithName("GetCurrentUser").RequireAuthorization()` returns `UserInfo`.
    5. `app.MapOpenApi(); app.MapUtcp("/utcp");` Add `public partial class Program;` at file end for WebApplicationFactory.
    6. launchSettings http profile on `http://localhost:5080`; `.http` file with requests.
  - **Pitfalls / non-goals**:
    - Unsecured operations must NOT get a security requirement (don't add a global `doc.SecurityRequirements`).
    - Use `TypedResults.Ok(...)` so response schemas are generated.
  - **Acceptance**:
    - `dotnet run --project samples/Utcp.Sample.Api` then `curl -s http://localhost:5080/utcp` returns JSON with 3 tools: GetWeather, CreateOrder, GetCurrentUser; GetCurrentUser has `auth`.
    - `curl -s http://localhost:5080/openapi/v1.json` shows bearer scheme.

- [x] 6. UTCP manual JSON Schema
  - **What**: Author draft 2020-12 schema for the manual.
  - **Files**: `tests/Utcp.AspNetCore.Tests/Schemas/utcp-manual.schema.json` (csproj: `CopyToOutputDirectory=PreserveNewest`)
  - **Depends on**: 1
  - **Implementation outline**:
    1. Root: required `manual_version`, `utcp_version` (string), `tools` (array of `$defs/tool`); `additionalProperties: false`.
    2. `$defs/tool`: required `name` (pattern `^[A-Za-z0-9_.-]+$`), `description`, `inputs` (`$defs/jsonSchemaObject`), `outputs`, `tool_call_template`; optional `tags` string array; no extra props.
    3. `$defs/httpCallTemplate`: required `call_template_type` const `http`, `url` (string, format uri-template or pattern `^https?://`), `http_method` enum GET/POST/PUT/DELETE/PATCH; optional `name`, `content_type`, `body_field`, `header_fields` (string array), `auth` (`$defs/apiKeyAuth`); `additionalProperties:false`.
    4. `$defs/apiKeyAuth`: `auth_type` const `api_key`, `api_key` string, `var_name` string, `location` enum header/query/cookie; all required.
    5. `jsonSchemaObject`: `type: object` (allow any keys).
  - **Pitfalls / non-goals**:
    - Any camelCase key (e.g. `toolCallTemplate`) must fail due to `additionalProperties:false` — this is how snake_case is enforced.
  - **Acceptance**:
    - File is valid JSON and loads via `JsonSchema.FromFile`.

- [x] 7. Integration tests
  - **What**: xUnit tests over WebApplicationFactory<Program>.
  - **Files**: `tests/Utcp.AspNetCore.Tests/UtcpEndpointTests.cs`, `tests/Utcp.AspNetCore.Tests/UtcpSchemaValidationTests.cs`
  - **Depends on**: 5, 6
  - **Implementation outline**:
    1. `IClassFixture<WebApplicationFactory<Program>>`; GET `/utcp`, parse with `JsonNode`.
    2. Schema test: `JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory,"Schemas","utcp-manual.schema.json")).Evaluate(node, new EvaluationOptions{ OutputFormat = OutputFormat.List })`; assert `IsValid`, print errors on failure.
    3. Asserts: tools count == 3 and names set == {GetWeather, CreateOrder, GetCurrentUser}; `/utcp` and `/openapi` not tools.
    4. GetCurrentUser `tool_call_template.auth` equals expected object exactly; other tools have no `auth` key.
    5. GetWeather url ends with `/weather/{city}`, starts with `http://localhost`, inputs.properties has `city` & `days`, `required` contains `city`, http_method GET.
    6. CreateOrder: `body_field == "body"`, inputs.properties.body exists with `product`/`quantity` props, http_method POST, content_type `application/json`.
    7. Snake_case: root keys exactly `manual_version`,`utcp_version`,`tools`; versions `1.0.0`/`1.0.1`; no key containing uppercase in tool/template objects.
  - **Pitfalls / non-goals**:
    - JwtBearer without authority config must not throw at startup in tests (no validation needed for /utcp).
    - Tests must not depend on calling `/openapi/v1.json` first.
  - **Acceptance**:
    - `dotnet test` all green.

- [x] 8. README
  - **What**: Usage docs.
  - **Files**: `README.md`
  - **Depends on**: 5, 7
  - **Implementation outline**:
    1. What/why, install (project ref), `AddOpenApi()+AddUtcp()+MapUtcp()` snippet, options table, mapping rules, bearer → auth mapping, sample run + curl, test command, limitations.
  - **Pitfalls / non-goals**:
    - Keep examples matching actual API names.
  - **Acceptance**:
    - Snippets compile-consistent with library.

- [x] 9. Final verification and git init
  - **What**: Full build/test, curl check, initial commit.
  - **Depends on**: 1–8
  - **Implementation outline**:
    1. `dotnet build` and `dotnet test`.
    2. Run sample, `curl -s http://localhost:5080/utcp | jq` and inspect; stop app.
    3. `git init && git add -A && git commit -m "Initial commit: Utcp.AspNetCore library, sample, tests"`.
  - **Pitfalls / non-goals**:
    - Ensure `bin/`/`obj/` excluded by .gitignore before `git add`.
  - **Acceptance**:
    - Commit exists; `git status` clean.

## Verification
- `dotnet build utcp-library.sln` → `Build succeeded. 0 Error(s)`.
- `dotnet test` → all tests passed, 0 failed.
- `dotnet run --project samples/Utcp.Sample.Api` + `curl -s http://localhost:5080/utcp | jq '.tools[].name'` → `"GetWeather" "CreateOrder" "GetCurrentUser"`; `jq '.tools[] | select(.name=="GetCurrentUser") .tool_call_template.auth'` → bearer api_key object.
- `git log --oneline` shows one commit.
