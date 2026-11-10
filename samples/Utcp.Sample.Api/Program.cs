using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Utcp.AspNetCore;
using Utcp.AspNetCore.Models;
using Utcp.Sample.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddScheme<DemoAuthOptions, ApiKeyHandler>(DemoSchemes.ApiKeyHeader,
        o => builder.Configuration.GetSection("DemoAuth").Bind(o))
    .AddScheme<DemoAuthOptions, ApiKeyHandler>(DemoSchemes.ApiKeyQuery,
        o => builder.Configuration.GetSection("DemoAuth").Bind(o))
    .AddScheme<DemoAuthOptions, BasicHandler>(DemoSchemes.Basic,
        o => builder.Configuration.GetSection("DemoAuth").Bind(o));
builder.Services.AddAuthorization(o =>
{
    foreach (var s in new[] { DemoSchemes.ApiKeyHeader, DemoSchemes.ApiKeyQuery, DemoSchemes.Basic })
        o.AddPolicy(s, p => p.AddAuthenticationSchemes(s).RequireAuthenticatedUser());
});

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    o.AddOperationTransformer<BearerOperationTransformer>();
});
builder.Services.AddControllers();
builder.Services.AddUtcp()
    .Register<SampleToolSource>()
    .Configure(ctx =>
    {
        // Post-process the final manual, e.g. tag the custom tool.
        ctx.Manual.Tools.Single(t => t.Name == "GetStatus").Tags.Add("Custom");
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

// Backing endpoint for the custom GetStatus tool; excluded from OpenAPI
// so it only appears in the manual via SampleToolSource.
app.MapGet("/status", () => TypedResults.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }))
    .ExcludeFromDescription();

app.MapGet("/weather/{city}", (string city, int? days) =>
    {
        var count = Math.Clamp(days ?? 3, 1, 14);
        var forecasts = Enumerable.Range(1, count)
            .Select(i => new WeatherForecast(city, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)), 15 + i, "Sunny"))
            .ToArray();
        return TypedResults.Ok(forecasts);
    })
    .WithName("GetWeather")
    .WithSummary("Get a multi-day weather forecast for a city")
    .WithTags("Weather");

app.MapPost("/orders", (CreateOrderRequest request) =>
        TypedResults.Ok(new Order(Guid.NewGuid(), request.Product, request.Quantity, "Created")))
    .WithName("CreateOrder")
    .WithSummary("Create a new order")
    .WithTags("Orders");

app.MapGet("/me", (ClaimsPrincipal user) =>
        TypedResults.Ok(new UserInfo(user.Identity?.Name ?? "unknown", user.Claims.Select(c => c.Type).ToArray())))
    .WithName("GetCurrentUser")
    .WithSummary("Get the current authenticated user")
    .WithTags("Users")
    .RequireAuthorization();

app.MapGet("/admin/stats", () => TypedResults.Ok(new { users = 42, orders = 1337 }))
    .WithName("GetAdminStats").WithSummary("Get admin statistics (API key in header)").WithTags("Admin")
    .RequireAuthorization(DemoSchemes.ApiKeyHeader);

app.MapGet("/reports", () => TypedResults.Ok(new[] { "Q1", "Q2", "Q3" }))
    .WithName("GetReports").WithSummary("List reports (API key in query string)").WithTags("Reports")
    .RequireAuthorization(DemoSchemes.ApiKeyQuery);

app.MapGet("/legacy/export", () => TypedResults.Ok(new { format = "csv", rows = 100 }))
    .WithName("GetLegacyExport").WithSummary("Legacy export (HTTP Basic)").WithTags("Legacy")
    .RequireAuthorization(DemoSchemes.Basic);

app.MapControllers();
app.MapUtcp();

app.Run();

public record WeatherForecast(string City, DateOnly Date, int TemperatureC, string Summary);

public record CreateOrderRequest(string Product, int Quantity);

public record Order(Guid Id, string Product, int Quantity, string Status);

public record UserInfo(string Name, string[] ClaimTypes);

/// <summary>Demonstrates <see cref="IUtcpToolSource" />: contributes a tool that is not in the OpenAPI document.</summary>
public sealed class SampleToolSource : IUtcpToolSource
{
    public ValueTask<IReadOnlyList<UtcpTool>> GetToolsAsync(UtcpToolSourceContext context,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UtcpTool> tools =
        [
            new UtcpTool
            {
                Name = "GetStatus",
                Description = "Get service status (custom tool source)",
                ToolCallTemplate = new HttpCallTemplate
                {
                    Name = "GetStatus", Url = context.BaseUrl + "/status", HttpMethod = "GET"
                }
            }
        ];
        return ValueTask.FromResult(tools);
    }
}

public partial class Program;
