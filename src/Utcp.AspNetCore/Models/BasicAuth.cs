namespace Utcp.AspNetCore.Models;

/// <summary>
///     UTCP <c>basic</c> auth (HTTP Basic). Both fields are <c>${PLACEHOLDER}</c> variables the agent fills in,
///     never real credentials.
/// </summary>
public sealed class BasicAuth : UtcpAuth
{
    /// <summary>JSON <c>auth_type</c>; always <c>"basic"</c>.</summary>
    public override string AuthType => "basic";

    /// <summary>JSON <c>username</c>: placeholder such as <c>${BASIC_USERNAME}</c> substituted by the agent.</summary>
    public string Username { get; set; } = "";

    /// <summary>JSON <c>password</c>: placeholder such as <c>${BASIC_PASSWORD}</c> substituted by the agent.</summary>
    public string Password { get; set; } = "";
}
