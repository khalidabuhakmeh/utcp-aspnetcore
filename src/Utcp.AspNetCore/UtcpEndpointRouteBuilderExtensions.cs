using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Utcp.AspNetCore;

public static class UtcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapUtcp(this IEndpointRouteBuilder endpoints, string pattern = "/utcp")
    {
        // Validate eagerly at mapping time so misconfiguration fails at startup with a clear message.
        if (endpoints.ServiceProvider.GetService<UtcpManualProvider>() is null)
            throw new InvalidOperationException(
                "MapUtcp() requires UTCP services. Call builder.Services.AddUtcp() before building the app.");

        var documentName = endpoints.ServiceProvider
            .GetRequiredService<IOptions<UtcpOptions>>().Value.DocumentName;
        if (!OpenApiRegistration.IsRegistered(endpoints.ServiceProvider, documentName))
            throw OpenApiRegistration.CreateMissingOpenApiException(documentName, "MapUtcp()");

        return endpoints.MapGet(pattern, async (HttpContext ctx) =>
        {
            var provider = ctx.RequestServices.GetRequiredService<UtcpManualProvider>();
            var manual = await provider.GetManualAsync(ctx);
            return Results.Json(manual, UtcpJson.SerializerOptions);
        }).ExcludeFromDescription();
    }
}
