using JetBrains.Annotations;

namespace Utcp.AspNetCore.Models;

/// <summary>
///     A UTCP manual: the discovery document an agent fetches to learn which tools an API offers and how to call them.
///     This is the payload served by the endpoint mapped with <c>MapUtcp</c>.
/// </summary>
[PublicAPI]
public sealed class UtcpManual
{
    /// <summary>JSON <c>manual_version</c>: version of this manual's content (from <see cref="UtcpOptions.ManualVersion" />).</summary>
    public string ManualVersion { get; set; } = "1.0.0";

    /// <summary>JSON <c>utcp_version</c>: version of the UTCP specification the manual conforms to.</summary>
    public string UtcpVersion { get; set; } = "1.0.1";

    /// <summary>JSON <c>tools</c>: one tool per OpenAPI operation that has an <c>operationId</c>.</summary>
    public List<UtcpTool> Tools { get; set; } = new();
}
