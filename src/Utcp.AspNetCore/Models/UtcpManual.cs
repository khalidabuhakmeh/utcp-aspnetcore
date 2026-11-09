namespace Utcp.AspNetCore.Models;

public sealed class UtcpManual
{
    public string ManualVersion { get; set; } = "1.0.0";
    public string UtcpVersion { get; set; } = "1.0.1";
    public List<UtcpTool> Tools { get; set; } = new();
}
