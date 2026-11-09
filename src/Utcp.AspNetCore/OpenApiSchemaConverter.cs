using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace Utcp.AspNetCore;

/// <summary>Converts OpenAPI schemas into self-contained JSON Schema objects for UTCP inputs/outputs.</summary>
public static class OpenApiSchemaConverter
{
    private const string RefPrefix = "#/components/schemas/";

    /// <summary>
    /// Serializes <paramref name="schema"/> to JSON and inlines <c>#/components/schemas/...</c> references from
    /// <paramref name="doc"/>, so the result needs no external definitions.
    /// </summary>
    /// <param name="schema">Schema to convert; <c>null</c> yields an empty object.</param>
    /// <param name="doc">Document whose components resolve references.</param>
    /// <returns>The inlined JSON Schema.</returns>
    public static JsonObject ToJsonObject(OpenApiSchema? schema, OpenApiDocument doc)
    {
        if (schema is null) return new JsonObject();
        var node = Serialize(schema);
        var resolved = Resolve(node, doc, new HashSet<string>());
        return resolved as JsonObject ?? new JsonObject();
    }

    private static JsonNode? Serialize(OpenApiSchema schema)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        var writer = new OpenApiJsonWriter(sw);
        schema.SerializeAsV3(writer);
        return JsonNode.Parse(sw.ToString());
    }

    private static JsonNode? Resolve(JsonNode? node, OpenApiDocument doc, HashSet<string> visiting)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["$ref"] is JsonNode r && r.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    var rs = r.GetValue<string>();
                    if (rs.StartsWith(RefPrefix, StringComparison.Ordinal))
                    {
                        var id = rs[RefPrefix.Length..];
                        if (visiting.Contains(id)) return obj.DeepClone(); // cycle: leave $ref
                        if (doc.Components?.Schemas is { } schemas && schemas.TryGetValue(id, out var target))
                        {
                            visiting.Add(id);
                            try
                            {
                                using var sw = new StringWriter(CultureInfo.InvariantCulture);
                                target.SerializeAsV3WithoutReference(new OpenApiJsonWriter(sw));
                                return Resolve(JsonNode.Parse(sw.ToString()), doc, visiting);
                            }
                            finally { visiting.Remove(id); }
                        }
                    }
                    return obj.DeepClone();
                }
                var result = new JsonObject();
                foreach (var kv in obj)
                    result[kv.Key] = Resolve(kv.Value, doc, visiting);
                return result;
            case JsonArray arr:
                var na = new JsonArray();
                foreach (var item in arr) na.Add(Resolve(item, doc, visiting));
                return na;
            default:
                return node?.DeepClone();
        }
    }
}
