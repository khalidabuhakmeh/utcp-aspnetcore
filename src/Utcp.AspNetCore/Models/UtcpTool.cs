using System.Text.Json.Nodes;

namespace Utcp.AspNetCore.Models;

public sealed class UtcpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public JsonObject Inputs { get; set; } = new();
    public JsonObject Outputs { get; set; } = new();
    public HttpCallTemplate ToolCallTemplate { get; set; } = new();
}
