using Microsoft.AspNetCore.Http;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore;

internal sealed class UtcpManualProvider
{
    private readonly UtcpOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OpenApiDocument? _doc;

    public UtcpManualProvider(IOptions<UtcpOptions> options) => _options = options.Value;

    public async Task<UtcpManual> GetManualAsync(HttpContext context)
    {
        var doc = await GetDocumentAsync(context.RequestServices);
        var req = context.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}{req.PathBase}";
        return OpenApiToUtcpConverter.Convert(doc, baseUrl, _options);
    }

    private async Task<OpenApiDocument> GetDocumentAsync(IServiceProvider sp)
    {
        if (_doc is not null) return _doc;
        await _gate.WaitAsync();
        try
        {
            if (_doc is not null) return _doc;
            // Both IDocumentProvider and OpenApiDocumentService are internal in Microsoft.AspNetCore.OpenApi 9.
            // IDocumentProvider resolves scoped services from the root provider (fails with scope validation),
            // so use the keyed OpenApiDocumentService with the request-scoped provider, like MapOpenApi does.
            var serviceType = typeof(Microsoft.AspNetCore.OpenApi.OpenApiOptions).Assembly
                .GetType("Microsoft.AspNetCore.OpenApi.OpenApiDocumentService");
            var service = serviceType is null ? null
                : (sp as IKeyedServiceProvider)?.GetKeyedService(serviceType, _options.DocumentName);
            var method = serviceType?.GetMethod("GetOpenApiDocumentAsync");
            if (service is null || method is null)
                throw new InvalidOperationException(
                    $"OpenAPI document '{_options.DocumentName}' is not registered. Call services.AddOpenApi() before using AddUtcp()/MapUtcp().");
            var args = method.GetParameters().Select(pi =>
                pi.ParameterType == typeof(IServiceProvider) ? sp
                : pi.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None
                : pi.ParameterType.IsValueType ? Activator.CreateInstance(pi.ParameterType) : null).ToArray();
            var task = (Task)method.Invoke(service, args)!;
            await task.ConfigureAwait(false);
            var generated = (OpenApiDocument)task.GetType().GetProperty("Result")!.GetValue(task)!;
            // Round-trip through JSON so the converter sees a fully resolved, reader-produced document.
            using var sw = new StringWriter();
            generated.SerializeAsV3(new Microsoft.OpenApi.Writers.OpenApiJsonWriter(sw));
            var parsed = new OpenApiStringReader().Read(sw.ToString(), out var diag);
            if (parsed is null || diag.Errors.Count > 0 && parsed.Paths is null)
                throw new InvalidOperationException("Failed to parse OpenAPI document: " +
                    string.Join("; ", diag.Errors.Select(e => e.Message)));
            _doc = parsed;
            return parsed;
        }
        finally { _gate.Release(); }
    }
}
