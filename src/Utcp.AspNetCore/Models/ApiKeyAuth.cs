namespace Utcp.AspNetCore.Models;

/// <summary>
/// UTCP <c>api_key</c> auth: a key (or bearer token) sent in a header, query string, or cookie.
/// Also used for HTTP bearer schemes, emitted as <c>Authorization: Bearer ${NAME_TOKEN}</c>.
/// </summary>
public sealed class ApiKeyAuth : UtcpAuth
{
    /// <summary>JSON <c>auth_type</c>; always <c>"api_key"</c>.</summary>
    public override string AuthType => "api_key";
    /// <summary>
    /// JSON <c>api_key</c>: the value to send, as a placeholder such as <c>${BEARER_TOKEN}</c> or
    /// <c>Bearer ${BEARER_TOKEN}</c>. The agent substitutes the real secret; never put a real secret here.
    /// </summary>
    public string ApiKey { get; set; } = "";
    /// <summary>JSON <c>var_name</c>: name of the header, query parameter, or cookie carrying the key (default <c>Authorization</c>).</summary>
    public string VarName { get; set; } = "Authorization";
    /// <summary>JSON <c>location</c>: where the key is sent; one of <c>header</c>, <c>query</c>, or <c>cookie</c>.</summary>
    public string Location { get; set; } = "header";
}
