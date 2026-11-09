using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore;

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

    public static UtcpManual Convert(OpenApiDocument doc, string baseUrl, UtcpOptions options)
    {
        var manual = new UtcpManual { ManualVersion = options.ManualVersion };
        foreach (var (pathKey, item) in doc.Paths)
        {
            foreach (var (opType, op) in item.Operations)
            {
                var method = MethodName(opType);
                if (method is null || string.IsNullOrEmpty(op.OperationId)) continue;
                manual.Tools.Add(BuildTool(doc, pathKey, item, method, op, baseUrl, options));
            }
        }
        return manual;
    }

    private static UtcpTool BuildTool(OpenApiDocument doc, string pathKey, OpenApiPathItem item, string method,
        OpenApiOperation op, string baseUrl, UtcpOptions o)
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

        template.Auth = BuildAuth(doc, op, o);

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

    private static ApiKeyAuth? BuildAuth(OpenApiDocument doc, OpenApiOperation op, UtcpOptions o)
    {
        IEnumerable<OpenApiSecurityRequirement> reqs =
            op.Security is { Count: > 0 } ? op.Security : doc.SecurityRequirements ?? new List<OpenApiSecurityRequirement>();
        foreach (var req in reqs)
            foreach (var scheme in req.Keys)
            {
                var real = scheme;
                if (scheme.Reference?.Id is { } id && doc.Components?.SecuritySchemes.TryGetValue(id, out var found) == true)
                    real = found;
                if (real.Type == SecuritySchemeType.Http &&
                    string.Equals(real.Scheme, "bearer", StringComparison.OrdinalIgnoreCase))
                    return new ApiKeyAuth { ApiKey = $"Bearer ${{{o.TokenVariableName}}}", VarName = "Authorization" };
            }
        return null;
    }
}
