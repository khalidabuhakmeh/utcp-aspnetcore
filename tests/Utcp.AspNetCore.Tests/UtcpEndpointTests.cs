using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Utcp.AspNetCore.Tests;

public class UtcpEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<JsonObject> GetManual() =>
        JsonNode.Parse(await factory.CreateClient().GetStringAsync("/utcp"))!.AsObject();

    private static JsonObject Tool(JsonObject m, string name) =>
        m["tools"]!.AsArray().Select(t => t!.AsObject()).Single(t => (string)t["name"]! == name);

    [Fact]
    public async Task Root_shape_and_versions()
    {
        var m = await GetManual();
        Assert.Equal(new[] { "manual_version", "tools", "utcp_version" }, m.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal("1.0.0", (string)m["manual_version"]!);
        Assert.Equal("1.0.1", (string)m["utcp_version"]!);
    }

    [Fact]
    public async Task Tool_names()
    {
        var m = await GetManual();
        var names = m["tools"]!.AsArray().Select(t => (string)t!["name"]!).ToHashSet();
        Assert.Equal(3, m["tools"]!.AsArray().Count);
        Assert.Equal(new HashSet<string> { "GetWeather", "CreateOrder", "GetCurrentUser" }, names);
    }

    [Fact]
    public async Task Auth_only_on_GetCurrentUser()
    {
        var m = await GetManual();
        var auth = Tool(m, "GetCurrentUser")["tool_call_template"]!["auth"]!.AsObject();
        Assert.Equal(4, auth.Count);
        Assert.Equal("api_key", (string)auth["auth_type"]!);
        Assert.Equal("Bearer ${API_TOKEN_VARNAME}", (string)auth["api_key"]!);
        Assert.Equal("Authorization", (string)auth["var_name"]!);
        Assert.Equal("header", (string)auth["location"]!);
        foreach (var n in new[] { "GetWeather", "CreateOrder" })
            Assert.False(Tool(m, n)["tool_call_template"]!.AsObject().ContainsKey("auth"));
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
                .Concat(o["tool_call_template"]!.AsObject().TryGetPropertyValue("auth", out var a) ? a!.AsObject().Select(p => p.Key) : []);
            Assert.All(keys, k => Assert.DoesNotContain(k, k.Where(char.IsUpper).Select(c => c.ToString())));
        }
    }
}
