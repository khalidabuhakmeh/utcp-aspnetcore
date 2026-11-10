using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore.Tests;

public class UtcpExtensibilityTests
{
    private static UtcpTool T(string name) => new() { Name = name, ToolCallTemplate = new HttpCallTemplate { Name = name } };

    private sealed class SourceA : IUtcpToolSource
    {
        public ValueTask<IReadOnlyList<UtcpTool>> GetToolsAsync(UtcpToolSourceContext c, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<UtcpTool>>([T("FromA")]);
    }

    private sealed class SourceB : IUtcpToolSource
    {
        public ValueTask<IReadOnlyList<UtcpTool>> GetToolsAsync(UtcpToolSourceContext c, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<UtcpTool>>([T("FromB")]);
    }

    private sealed class DupSource(string name) : IUtcpToolSource
    {
        public ValueTask<IReadOnlyList<UtcpTool>> GetToolsAsync(UtcpToolSourceContext c, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<UtcpTool>>([T(name)]);
    }

    private static async Task<JsonObject> Get(Action<UtcpBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenApi();
        configure(builder.Services.AddUtcp());
        await using var app = builder.Build();
        app.MapGet("/ping", () => "pong").WithName("Ping");
        app.MapUtcp();
        await app.StartAsync();
        var json = await app.GetTestClient().GetStringAsync("/utcp");
        return JsonNode.Parse(json)!.AsObject();
    }

    private static string[] Names(JsonObject m) =>
        m["tools"]!.AsArray().Select(t => (string)t!["name"]!).ToArray();

    [Fact]
    public async Task Single_source_contributes()
    {
        var m = await Get(b => b.Register<SourceA>());
        Assert.Equal(["Ping", "FromA"], Names(m));
    }

    [Fact]
    public async Task Multiple_sources_contribute_in_order()
    {
        var m = await Get(b => b.Register<SourceA>().Register(new SourceB()));
        Assert.Equal(["Ping", "FromA", "FromB"], Names(m));
    }

    [Fact]
    public async Task Same_source_type_twice_is_not_duplicated()
    {
        var m = await Get(b => b.Register<SourceA>().Register<SourceA>());
        Assert.Equal(["Ping", "FromA"], Names(m));
    }

    [Fact]
    public async Task Source_colliding_with_openapi_throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Get(b => b.Register(new DupSource("Ping"))));
        Assert.Contains("'Ping'", ex.Message);
    }

    [Fact]
    public async Task Source_colliding_with_source_throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Get(b => b.Register<SourceA>().Register(new DupSource("FromA"))));
        Assert.Contains("'FromA'", ex.Message);
    }

    [Fact]
    public async Task Duplicate_openapi_operation_ids_throw()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenApi(o => o.AddOperationTransformer((op, _, _) =>
        {
            op.OperationId = "Same";
            return Task.CompletedTask;
        }));
        builder.Services.AddUtcp();
        await using var app = builder.Build();
        app.MapGet("/a", () => "a");
        app.MapGet("/b", () => "b");
        app.MapUtcp();
        await app.StartAsync();
        var resp = await Record.ExceptionAsync(() => app.GetTestClient().GetStringAsync("/utcp"));
        var ioe = Assert.IsType<InvalidOperationException>(resp);
        Assert.Contains("'Same'", ioe.Message);
    }

    [Fact]
    public async Task Direct_manual_tools_mutation_duplicate_throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Get(b => b.Configure(ctx => ctx.Manual.Tools.Add(T("Ping")))));
        Assert.Contains("'Ping'", ex.Message);
    }

    [Fact]
    public async Task Configure_hooks_run_in_order_after_sources()
    {
        var log = new List<string>();
        var m = await Get(b => b
            .Register<SourceA>()
            .Configure(ctx =>
            {
                log.Add("sync1");
                ctx.Add(T("S1"));
            })
            .Configure(async (ctx, ct) =>
            {
                await Task.Yield();
                log.Add("async2");
                Assert.Contains("S1", ctx.Manual.Tools.Select(t => t.Name));
                ctx.Add(T("S2"));
            })
            .Configure(ctx =>
            {
                log.Add("sync3");
                ctx.Remove("FromA");
            }));
        Assert.Equal(["sync1", "async2", "sync3"], log);
        Assert.Equal(["Ping", "S1", "S2"], Names(m));
    }

    private static UtcpManualContext Ctx(params string[] names)
    {
        var manual = new UtcpManual { Tools = names.Select(T).ToList() };
        return new UtcpManualContext(manual, new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public void Add_throws_on_duplicate()
    {
        var ctx = Ctx("a");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Add(T("a")));
        Assert.Contains("'a'", ex.Message);
        ctx.Add(T("b"));
        Assert.Equal(2, ctx.Manual.Tools.Count);
    }

    [Fact]
    public void Remove_returns_whether_found()
    {
        var ctx = Ctx("a");
        Assert.True(ctx.Remove("a"));
        Assert.False(ctx.Remove("a"));
        Assert.Empty(ctx.Manual.Tools);
    }

    [Fact]
    public void Replace_works_and_throws_when_missing()
    {
        var ctx = Ctx("a", "b");
        var n = T("b");
        n.Description = "new";
        ctx.Replace(n);
        Assert.Same(n, ctx.Manual.Tools[1]);
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Replace(T("zzz")));
        Assert.Contains("'zzz'", ex.Message);
    }
}
