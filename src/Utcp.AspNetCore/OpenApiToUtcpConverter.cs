using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore;

/// <summary>
/// Converts an OpenAPI document into a <see cref="UtcpManual"/>.
/// </summary>
/// <remarks>
/// Rules: each operation with an <c>operationId</c> (GET/POST/PUT/DELETE/PATCH) becomes a tool named by it;
/// path/query/header parameters plus the request body become <c>inputs</c>; the 200 (else first 2xx) response schema
/// becomes <c>outputs</c>; security schemes (operation-level, else document-level) become <c>auth</c> with
/// <c>${...}</c> placeholders; <c>servers[0]</c> is the base URL, falling back to the caller-supplied URL when
/// absent or not absolute http(s).
/// </remarks>
public static class OpenApiToUtcpConverter
{
    private static string? MethodName(OperationType t) => t switch
    {
        OperationType.Get => "GET",
        OperationType.Post => "POST",
        OperationType.Put => "PUT",
        OperationType.Delete => "DELETE",
        OperationType.Patch => "PATCH",
        _ => null,
    };

    /// <summary>Builds the manual for <paramref name="doc"/>.</summary>
    /// <param name="doc">The OpenAPI document to convert.</param>
    /// <param name="fallbackBaseUrl">Base URL used when the document has no usable <c>servers[0]</c> (typically the request origin).</param>
    /// <param name="options">Supplies the emitted <c>manual_version</c>.</param>
    /// <returns>The generated manual.</returns>
    public static UtcpManual Convert(OpenApiDocument doc, string fallbackBaseUrl, UtcpOptions options)
    {
        var baseUrl = ResolveBaseUrl(doc, fallbackBaseUrl);
        var manual = new UtcpManual { ManualVersion = options.ManualVersion };
        foreach (var (pathKey, item) in doc.Paths)
        {
            foreach (var (opType, op) in item.Operations)
            {
                var method = MethodName(opType);
                if (method is null || string.IsNullOrEmpty(op.OperationId)) continue;
                manual.Tools.Add(BuildTool(doc, pathKey, item, method, op, baseUrl));
            }
        }
        return manual;
    }

    private static UtcpTool BuildTool(OpenApiDocument doc, string pathKey, OpenApiPathItem item, string method,
        OpenApiOperation op, string baseUrl)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        List<string>? headerFields = null;

        // path-level parameters overridden by operation-level
        var parameters = item.Parameters.Where(p => !op.Parameters.Any(q => q.Name == p.Name && q.In == p.In))
            .Concat(op.Parameters);
        foreach (var p in parameters)
        {
            if (p.In is not (ParameterLocation.Path or ParameterLocation.Query or ParameterLocation.Header)) continue;
            var schema = OpenApiSchemaConverter.ToJsonObject(p.Schema, doc);
            if (!string.IsNullOrEmpty(p.Description)) schema["description"] = p.Description;
            props[p.Name] = schema;
            if (p.Required || p.In == ParameterLocation.Path) required.Add(p.Name);
            if (p.In == ParameterLocation.Header) (headerFields ??= new()).Add(p.Name);
        }

        var template = new HttpCallTemplate
        {
            Name = op.OperationId,
            Url = baseUrl.TrimEnd('/') + pathKey,
            HttpMethod = method,
            HeaderFields = headerFields,
        };

        if (op.RequestBody?.Content is { Count: > 0 } content)
        {
            var key = content.ContainsKey("application/json") ? "application/json" : content.Keys.First();
            props["body"] = OpenApiSchemaConverter.ToJsonObject(content[key].Schema, doc);
            if (op.RequestBody.Required) required.Add("body");
            template.BodyField = "body";
            template.ContentType = key;
        }

        var inputs = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (required.Count > 0) inputs["required"] = required;

        var outputs = new JsonObject { ["type"] = "object" };
        var resp = op.Responses.TryGetValue("200", out var r200) ? r200
            : op.Responses.FirstOrDefault(kv => kv.Key.StartsWith('2')).Value;
        if (resp?.Content is { Count: > 0 } rc)
        {
            var rk = rc.ContainsKey("application/json") ? "application/json" : rc.Keys.First();
            if (rc[rk].Schema is { } s) outputs = OpenApiSchemaConverter.ToJsonObject(s, doc);
        }

        template.Auth = BuildAuth(doc, op);

        return new UtcpTool
        {
            Name = op.OperationId,
            Description = op.Summary ?? op.Description ?? "",
            Tags = op.Tags.Select(t => t.Name).ToList(),
            Inputs = inputs,
            Outputs = outputs,
            ToolCallTemplate = template,
        };
    }

    private static string ResolveBaseUrl(OpenApiDocument doc, string fallback)
    {
        var url = doc.Servers?.FirstOrDefault()?.Url;
        return !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var u) &&
               (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
            ? url
            : fallback;
    }

    private static string VarPrefix(string id)
    {
        var chars = id.ToUpperInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    private static object? BuildAuth(OpenApiDocument doc, OpenApiOperation op)
    {
        IEnumerable<OpenApiSecurityRequirement> reqs =
            op.Security is { Count: > 0 } ? op.Security : doc.SecurityRequirements ?? new List<OpenApiSecurityRequirement>();
        foreach (var req in reqs)
            foreach (var scheme in req.Keys)
            {
                var real = scheme;
                var id = scheme.Reference?.Id;
                if (id is not null && doc.Components?.SecuritySchemes.TryGetValue(id, out var found) == true)
                    real = found;
                if (id is null) continue;
                var v = VarPrefix(id);
                if (real.Type == SecuritySchemeType.Http &&
                    string.Equals(real.Scheme, "bearer", StringComparison.OrdinalIgnoreCase))
                    return new ApiKeyAuth { ApiKey = $"Bearer ${{{v}_TOKEN}}", VarName = "Authorization" };
                if (real.Type == SecuritySchemeType.Http &&
                    string.Equals(real.Scheme, "basic", StringComparison.OrdinalIgnoreCase))
                    return new BasicAuth { Username = $"${{{v}_USERNAME}}", Password = $"${{{v}_PASSWORD}}" };
                if (real.Type == SecuritySchemeType.ApiKey && !string.IsNullOrEmpty(real.Name))
                    return new ApiKeyAuth
                    {
                        ApiKey = $"${{{v}_API_KEY}}",
                        VarName = real.Name,
                        Location = real.In switch
                        {
                            ParameterLocation.Query => "query",
                            ParameterLocation.Cookie => "cookie",
                            _ => "header",
                        },
                    };
            }
        return null;
    }
}
