namespace Utcp.AspNetCore;

/// <summary>
/// Everything else (base URL, auth) is derived from the OpenAPI document: the base URL from <c>servers[0]</c>
/// (falling back to the request's scheme/host/pathBase) and auth from the document's security schemes.
/// </summary>
public sealed class UtcpOptions
{
    /// <summary>Name of the OpenAPI document to convert (as passed to AddOpenApi).</summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>Value emitted as the manual's <c>manual_version</c>.</summary>
    public string ManualVersion { get; set; } = "1.0.0";
}
