using System.Text.Json;
using System.Text.Json.Serialization;

namespace Utcp.AspNetCore;

/// <summary>
///     Shared JSON settings used to serialize UTCP manuals so the wire format matches the UTCP spec.
/// </summary>
public static class UtcpJson
{
    /// <summary>
    ///     Serializer options producing <c>snake_case</c> property names, omitting <c>null</c> values (so optional
    ///     UTCP fields such as <c>body_field</c> disappear rather than appearing as <c>null</c>), and indenting output.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}
