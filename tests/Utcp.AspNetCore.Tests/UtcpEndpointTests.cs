using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Utcp.AspNetCore.Tests;

public class UtcpEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<JsonObject> GetManual()
    {
        return JsonNode.Parse(await factory.CreateClient().GetStringAsync("/utcp"))!.AsObject();
    }

    private static JsonObject Tool(JsonObject m, string name)
    {
        return m["tools"]!.AsArray().Select(t => t!.AsObject()).Single(t => (string)t["name"]! == name);
    }

    [Fact]
    public async Task Root_shape_and_versions()
    {
        var m = await GetManual();
        Assert.Equal(new[] { "manual_version", "tools", "utcp_version" },
            m.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal("1.0.0", (string)m["manual_version"]!);
        Assert.Equal("1.0.1", (string)m["utcp_version"]!);
    }

    [Fact]
    public async Task Tool_names()
    {
        var m = await GetManual();
        var names = m["tools"]!.AsArray().Select(t => (string)t!["name"]!).ToHashSet();
        Assert.Equal(8, m["tools"]!.AsArray().Count);
        Assert.Equal(
            new HashSet<string>
                { "GetWeather", "CreateOrder", "GetCurrentUser", "GetAdminStats", "GetReports", "GetLegacyExport", "GetProduct", "GetStatus" },
            names);
    }

    [Fact]
    public async Task Controller_action_maps_to_tool()
    {
        var m = await GetManual();
        var t = Tool(m, "GetProduct");
        var tpl = t["tool_call_template"]!;
        Assert.EndsWith("/products/{id}", (string)tpl["url"]!);
        Assert.Equal("GET", (string)tpl["http_method"]!);
        Assert.Equal("Get a product by id", (string)t["description"]!);
        Assert.Equal(new[] { "Products" }, t["tags"]!.AsArray().Select(x => (string)x!).ToArray());
        var props = t["inputs"]!["properties"]!;
        Assert.Equal("integer", (string)props["id"]!["type"]!);
        Assert.Equal("boolean", (string)props["includePricing"]!["type"]!);
        var req = t["inputs"]!["required"]!.AsArray().Select(x => (string)x!).ToArray();
        Assert.Contains("id", req);
        Assert.DoesNotContain("includePricing", req);
    }

    [Fact]
    public async Task Bearer_auth_on_GetCurrentUser_and_none_on_public()
    {
        var m = await GetManual();
        var auth = Tool(m, "GetCurrentUser")["tool_call_template"]!["auth"]!.AsObject();
        Assert.Equal(4, auth.Count);
        Assert.Equal("api_key", (string)auth["auth_type"]!);
        Assert.Equal("Bearer ${BEARER_TOKEN}", (string)auth["api_key"]!);
        Assert.Equal("Authorization", (string)auth["var_name"]!);
        Assert.Equal("header", (string)auth["location"]!);
        foreach (var n in new[] { "GetWeather", "CreateOrder" })
            Assert.False(Tool(m, n)["tool_call_template"]!.AsObject().ContainsKey("auth"));
    }

    [Fact]
    public async Task Api_key_and_basic_auth_objects()
    {
        var m = await GetManual();
        var h = Tool(m, "GetAdminStats")["tool_call_template"]!["auth"]!.AsObject();
        Assert.Equal(4, h.Count);
        Assert.Equal("api_key", (string)h["auth_type"]!);
        Assert.Equal("${APIKEYHEADER_API_KEY}", (string)h["api_key"]!);
        Assert.Equal("X-Api-Key", (string)h["var_name"]!);
        Assert.Equal("header", (string)h["location"]!);

        var q = Tool(m, "GetReports")["tool_call_template"]!["auth"]!.AsObject();
        Assert.Equal(4, q.Count);
        Assert.Equal("api_key", (string)q["auth_type"]!);
        Assert.Equal("${APIKEYQUERY_API_KEY}", (string)q["api_key"]!);
        Assert.Equal("api_key", (string)q["var_name"]!);
        Assert.Equal("query", (string)q["location"]!);

        var b = Tool(m, "GetLegacyExport")["tool_call_template"]!["auth"]!.AsObject();
        Assert.Equal(3, b.Count);
        Assert.Equal("basic", (string)b["auth_type"]!);
        Assert.Equal("${BASIC_USERNAME}", (string)b["username"]!);
        Assert.Equal("${BASIC_PASSWORD}", (string)b["password"]!);
    }

    [Fact]
    public async Task GetWeather_details()
    {
        var t = Tool(await GetManual(), "GetWeather");
        var tpl = t["tool_call_template"]!;
        var url = (string)tpl["url"]!;
        Assert.StartsWith("http://localhost", url);
        Assert.EndsWith("/weather/{city}", url);
        Assert.Equal("GET", (string)tpl["http_method"]!);
        var props = t["inputs"]!["properties"]!.AsObject();
        Assert.True(props.ContainsKey("city"));
        Assert.True(props.ContainsKey("days"));
        Assert.Equal(new[] { "city" }, t["inputs"]!["required"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public async Task CreateOrder_details()
    {
        var t = Tool(await GetManual(), "CreateOrder");
        var tpl = t["tool_call_template"]!;
        Assert.Equal("body", (string)tpl["body_field"]!);
        Assert.Equal("POST", (string)tpl["http_method"]!);
        Assert.Equal("application/json", (string)tpl["content_type"]!);
        var body = t["inputs"]!["properties"]!["body"]!["properties"]!.AsObject();
        Assert.True(body.ContainsKey("product"));
        Assert.True(body.ContainsKey("quantity"));
    }

    [Fact]
    public async Task Structural_keys_are_snake_case()
    {
        var m = await GetManual();
        foreach (var t in m["tools"]!.AsArray())
        {
            var o = t!.AsObject();
            var keys = o.Select(p => p.Key)
                .Concat(o["tool_call_template"]!.AsObject().Select(p => p.Key))
                .Concat(o["tool_call_template"]!.AsObject().TryGetPropertyValue("auth", out var a)
                    ? a!.AsObject().Select(p => p.Key)
                    : []);
            Assert.All(keys, k => Assert.DoesNotContain(k, k.Where(char.IsUpper).Select(c => c.ToString())));
        }
    }
}
