namespace Utcp.AspNetCore.Models;

public sealed class HttpCallTemplate
{
    public string Name { get; set; } = "";
    public string CallTemplateType { get; set; } = "http";
    public string Url { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public string ContentType { get; set; } = "application/json";
    public string? BodyField { get; set; }
    public List<string>? HeaderFields { get; set; }
    public object? Auth { get; set; }
}
