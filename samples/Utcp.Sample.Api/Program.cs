using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Utcp.AspNetCore;
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
builder.Services.AddUtcp();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

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

public partial class Program;
