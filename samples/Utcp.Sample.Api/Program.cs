using Microsoft.AspNetCore.Authentication.JwtBearer;
using Utcp.AspNetCore;
using Utcp.Sample.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    o.AddOperationTransformer<BearerOperationTransformer>();
});
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

app.MapGet("/me", (System.Security.Claims.ClaimsPrincipal user) =>
    TypedResults.Ok(new UserInfo(user.Identity?.Name ?? "unknown", user.Claims.Select(c => c.Type).ToArray())))
.WithName("GetCurrentUser")
.WithSummary("Get the current authenticated user")
.WithTags("Users")
.RequireAuthorization();

app.MapUtcp("/utcp");

app.Run();

public record WeatherForecast(string City, DateOnly Date, int TemperatureC, string Summary);
public record CreateOrderRequest(string Product, int Quantity);
public record Order(Guid Id, string Product, int Quantity, string Status);
public record UserInfo(string Name, string[] ClaimTypes);

public partial class Program;
