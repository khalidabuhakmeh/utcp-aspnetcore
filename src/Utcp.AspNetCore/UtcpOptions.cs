namespace Utcp.AspNetCore;

/// <summary>
/// Options for <c>AddUtcp</c>. Only the source document and manual version are configurable.
/// Everything else (base URL, auth) is derived from the OpenAPI document: the base URL from <c>servers[0]</c>
/// (falling back to the request's scheme/host/pathBase) and auth from the document's security schemes.
/// </summary>
public sealed class UtcpOptions
{
    /// <summary>
    /// Name of the OpenAPI document to convert. Must match the name passed to <c>AddOpenApi</c> (default <c>"v1"</c>,
    /// which is what <c>AddOpenApi()</c> uses); a mismatch fails host startup.
    /// </summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>Value emitted as the manual's <c>manual_version</c>; bump it when your API's tool surface changes (default <c>"1.0.0"</c>).</summary>
    public string ManualVersion { get; set; } = "1.0.0";
}
