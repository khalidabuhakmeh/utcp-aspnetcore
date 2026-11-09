using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Utcp.AspNetCore;

/// <summary>Endpoint mapping for the UTCP manual.</summary>
[PublicAPI]
public static class UtcpEndpointRouteBuilderExtensions
{
    /// <summary>
    ///     Maps a GET endpoint (default <c>/utcp</c>) that returns the UTCP manual as JSON with <c>snake_case</c> field
    ///     names, generated from the app's OpenAPI document. The endpoint itself is excluded from that OpenAPI document.
    /// </summary>
    /// <remarks>
    ///     Configuration is validated eagerly at map time so mistakes fail at startup with a clear message.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder (for example the <c>WebApplication</c>).</param>
    /// <param name="pattern">Route pattern for the manual; defaults to <c>/utcp</c>.</param>
    /// <returns>
    ///     An <see cref="IEndpointConventionBuilder" /> for further configuration, e.g. <c>.RequireAuthorization()</c>
    ///     or <c>.AllowAnonymous()</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException"><c>AddUtcp()</c> was not called.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The OpenAPI document named by <see cref="UtcpOptions.DocumentName" /> is
    ///     not registered (missing <c>AddOpenApi()</c>).
    /// </exception>
    /// <example>
    ///     <code>
    /// app.MapUtcp().AllowAnonymous();
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapUtcp(this IEndpointRouteBuilder endpoints, string pattern = "/utcp")
    {
        // Validate eagerly at mapping time so misconfiguration fails at startup with a clear message.
        if (endpoints.ServiceProvider.GetService<UtcpManualProvider>() is null)
        {
            throw new InvalidOperationException(
                "MapUtcp() requires UTCP services. Call builder.Services.AddUtcp() before building the app.");
        }

        var documentName = endpoints.ServiceProvider
            .GetRequiredService<IOptions<UtcpOptions>>().Value.DocumentName;
        if (!OpenApiRegistration.IsRegistered(endpoints.ServiceProvider, documentName))
        {
            throw OpenApiRegistration.CreateMissingOpenApiException(documentName, "MapUtcp()");
        }

        return endpoints.MapGet(pattern, async (HttpContext ctx) =>
        {
            var provider = ctx.RequestServices.GetRequiredService<UtcpManualProvider>();
            var manual = await provider.GetManualAsync(ctx);
            return Results.Json(manual, UtcpJson.SerializerOptions);
        }).ExcludeFromDescription();
    }
}
