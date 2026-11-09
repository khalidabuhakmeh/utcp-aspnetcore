using JetBrains.Annotations;

namespace Utcp.AspNetCore.Models;

/// <summary>
///     UTCP HTTP call template: tells an agent how to invoke a tool over HTTP (URL, verb, body and header mapping, auth).
/// </summary>
[PublicAPI]
public sealed class HttpCallTemplate
{
    /// <summary>JSON <c>name</c>: call template name; set to the operation's <c>operationId</c>.</summary>
    public string Name { get; set; } = "";

    /// <summary>JSON <c>call_template_type</c>: UTCP protocol discriminator; always <c>"http"</c>.</summary>
    public string CallTemplateType { get; set; } = "http";

    /// <summary>
    ///     JSON <c>url</c>: absolute URL, the base URL (OpenAPI <c>servers[0]</c> or the request origin) plus the path.
    ///     Path templates such as <c>/orders/{id}</c> are kept; the agent fills them from the matching input properties.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>JSON <c>http_method</c>: upper-case verb (GET, POST, PUT, DELETE, PATCH).</summary>
    public string HttpMethod { get; set; } = "GET";

    /// <summary>
    ///     JSON <c>content_type</c>: media type of the request body (the OpenAPI request body content type; default
    ///     <c>application/json</c>).
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    ///     JSON <c>body_field</c>: name of the input property whose value is sent as the request body.
    ///     <c>null</c> (omitted) when the operation has no body.
    /// </summary>
    public string? BodyField { get; set; }

    /// <summary>
    ///     JSON <c>header_fields</c>: names of input properties to send as HTTP headers instead of query/path values.
    ///     <c>null</c> (omitted) when there are none.
    /// </summary>
    public List<string>? HeaderFields { get; set; }

    /// <summary>
    ///     JSON <c>auth</c>: a <see cref="ApiKeyAuth" /> or <see cref="BasicAuth" /> derived from the OpenAPI security
    ///     requirements, or <c>null</c> (omitted) for anonymous operations.
    /// </summary>
    public object? Auth { get; set; }
}
