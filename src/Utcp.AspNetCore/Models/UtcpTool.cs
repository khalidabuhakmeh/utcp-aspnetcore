using System.Text.Json.Nodes;

namespace Utcp.AspNetCore.Models;

/// <summary>A single callable tool in a <see cref="UtcpManual"/>, generated from one OpenAPI operation.</summary>
public sealed class UtcpTool
{
    /// <summary>JSON <c>name</c>: unique tool name; the operation's <c>operationId</c>.</summary>
    public string Name { get; set; } = "";
    /// <summary>JSON <c>description</c>: the operation's summary, falling back to its description, else empty. Agents use this to decide when to call the tool.</summary>
    public string Description { get; set; } = "";
    /// <summary>JSON <c>tags</c>: the operation's OpenAPI tag names, useful for grouping/searching tools.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>
    /// JSON <c>inputs</c>: JSON Schema object describing arguments. Path, query, and header parameters become
    /// properties by name; a request body becomes a single <c>body</c> property (see <see cref="HttpCallTemplate.BodyField"/>).
    /// </summary>
    public JsonObject Inputs { get; set; } = new();
    /// <summary>JSON <c>outputs</c>: JSON Schema of the successful (200/2xx) response body; a bare <c>{"type":"object"}</c> when none is declared.</summary>
    public JsonObject Outputs { get; set; } = new();
    /// <summary>JSON <c>tool_call_template</c>: how to perform the HTTP call for this tool.</summary>
    public HttpCallTemplate ToolCallTemplate { get; set; } = new();
}
