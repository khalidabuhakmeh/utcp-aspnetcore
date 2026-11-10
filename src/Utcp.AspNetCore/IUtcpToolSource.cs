using JetBrains.Annotations;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore;

/// <summary>
///     Contributes additional <see cref="UtcpTool" />s to the manual served at <c>/utcp</c>, after the OpenAPI-derived
///     tools have been generated. Register implementations with <see cref="UtcpBuilder.Register{TSource}" />.
/// </summary>
[PublicAPI]
public interface IUtcpToolSource
{
    /// <summary>Returns the tools to add to the manual. Names must not collide with any other tool.</summary>
    /// <param name="context">Details about the current manual request.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    ValueTask<IReadOnlyList<UtcpTool>> GetToolsAsync(UtcpToolSourceContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Information passed to an <see cref="IUtcpToolSource" /> when building the manual.</summary>
/// <param name="BaseUrl">The request-derived base URL (scheme, host, and path base) of the application.</param>
/// <param name="Options">The configured <see cref="UtcpOptions" />.</param>
/// <param name="Services">The request-scoped service provider.</param>
[PublicAPI]
public sealed record UtcpToolSourceContext(string BaseUrl, UtcpOptions Options, IServiceProvider Services);
