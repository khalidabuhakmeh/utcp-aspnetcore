using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Utcp.Sample.Api;

/// <summary>Registers the "Bearer" security scheme on the document.</summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header
        };
        document.Components.SecuritySchemes[DemoSchemes.ApiKeyHeader] = new OpenApiSecurityScheme
            { Type = SecuritySchemeType.ApiKey, Name = "X-Api-Key", In = ParameterLocation.Header };
        document.Components.SecuritySchemes[DemoSchemes.ApiKeyQuery] = new OpenApiSecurityScheme
            { Type = SecuritySchemeType.ApiKey, Name = "api_key", In = ParameterLocation.Query };
        document.Components.SecuritySchemes[DemoSchemes.Basic] = new OpenApiSecurityScheme
            { Type = SecuritySchemeType.Http, Scheme = "basic" };
        return Task.CompletedTask;
    }
}

/// <summary>Adds the Bearer requirement only to operations that carry IAuthorizeData metadata.</summary>
public sealed class BearerOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] Known = [DemoSchemes.ApiKeyHeader, DemoSchemes.ApiKeyQuery, DemoSchemes.Basic];

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var meta = context.Description.ActionDescriptor.EndpointMetadata;
        var authData = meta.OfType<IAuthorizeData>().ToList();
        if (authData.Count == 0) return Task.CompletedTask;

        // Match explicit schemes or policy names against registered demo schemes; default to Bearer.
        var id = authData
            .SelectMany(a => (a.AuthenticationSchemes ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append(a.Policy ?? ""))
            .FirstOrDefault(n => Known.Contains(n)) ?? "Bearer";

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id }
            }] = Array.Empty<string>()
        });
        return Task.CompletedTask;
    }
}
