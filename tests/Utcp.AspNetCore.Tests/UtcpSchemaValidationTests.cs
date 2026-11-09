using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Utcp.AspNetCore.Tests;

public class UtcpSchemaValidationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Manual_is_valid_against_schema()
    {
        var client = factory.CreateClient();
        var node = JsonNode.Parse(await client.GetStringAsync("/utcp"))!;
        var schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "Schemas", "utcp-manual.schema.json"));
        var result = schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        var errors = result.IsValid
            ? ""
            : string.Join("\n", result.Details.Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} {e.Value}")));
        Assert.True(result.IsValid, errors);
    }

    [Fact]
    public void Schema_rejects_camel_case_keys()
    {
        var schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "Schemas", "utcp-manual.schema.json"));
        var bad = JsonNode.Parse(
            """{"manual_version":"1","utcp_version":"1","tools":[{"name":"a","description":"d","inputs":{},"outputs":{},"toolCallTemplate":{}}]}""");
        Assert.False(schema.Evaluate(bad).IsValid);
    }
}
