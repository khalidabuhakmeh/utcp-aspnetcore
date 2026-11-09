namespace Utcp.AspNetCore.Models;

public sealed class BasicAuth : UtcpAuth
{
    public override string AuthType => "basic";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
