namespace Utcp.AspNetCore.Models;

public sealed class ApiKeyAuth
{
    public string AuthType { get; set; } = "api_key";
    public string ApiKey { get; set; } = "";
    public string VarName { get; set; } = "Authorization";
    public string Location { get; set; } = "header";
}
