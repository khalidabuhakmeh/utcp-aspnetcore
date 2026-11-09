using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Utcp.AspNetCore;

public static class UtcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapUtcp(this IEndpointRouteBuilder endpoints, string pattern = "/utcp")
    {
        return endpoints.MapGet(pattern, async (HttpContext ctx) =>
        {
            var provider = ctx.RequestServices.GetRequiredService<UtcpManualProvider>();
            var manual = await provider.GetManualAsync(ctx);
            return Results.Json(manual, UtcpJson.SerializerOptions);
        }).ExcludeFromDescription();
    }
}
