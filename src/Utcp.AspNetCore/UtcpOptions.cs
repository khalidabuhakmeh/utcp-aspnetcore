namespace Utcp.AspNetCore;

public sealed class UtcpOptions
{
    public string DocumentName { get; set; } = "v1";
    public string? BaseUrl { get; set; }
    public string ManualVersion { get; set; } = "1.0.0";
    public string TokenVariableName { get; set; } = "API_TOKEN_VARNAME";
}
