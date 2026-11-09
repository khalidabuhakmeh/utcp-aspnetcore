using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utcp.AspNetCore;

namespace Utcp.AspNetCore.Tests;

public class RegistrationValidationTests
{
    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        return builder;
    }

    [Fact]
    public void MapUtcp_WithoutAddUtcp_ThrowsClearError()
    {
        var builder = CreateBuilder();
        builder.Services.AddOpenApi();
        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => app.MapUtcp());
        Assert.Contains("AddUtcp()", ex.Message);
    }

    [Fact]
    public void MapUtcp_WithoutAddOpenApi_ThrowsClearError()
    {
        var builder = CreateBuilder();
        builder.Services.AddUtcp();
        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => app.MapUtcp());
        Assert.Contains("AddOpenApi()", ex.Message);
        Assert.Contains("'v1'", ex.Message);
    }

    [Fact]
    public void MapUtcp_WithMismatchedDocumentName_ThrowsClearError()
    {
        var builder = CreateBuilder();
        builder.Services.AddOpenApi("v1");
        builder.Services.AddUtcp(o => o.DocumentName = "internal");
        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => app.MapUtcp());
        Assert.Contains("'internal'", ex.Message);
        Assert.Contains("UtcpOptions.DocumentName", ex.Message);
    }

    [Fact]
    public async Task AddUtcp_WithoutAddOpenApi_FailsAtStartup()
    {
        var builder = CreateBuilder();
        builder.Services.AddUtcp();
        await using var app = builder.Build();
        // Note: MapUtcp is intentionally not called; AddUtcp alone must still fail fast.

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("AddOpenApi()", ex.Message);
    }

    [Fact]
    public async Task AddUtcp_WithAddOpenApi_StartsAndMapsSuccessfully()
    {
        var builder = CreateBuilder();
        builder.Services.AddOpenApi();
        builder.Services.AddUtcp();
        await using var app = builder.Build();
        app.MapUtcp();

        await app.StartAsync();
        await app.StopAsync();
    }
}
