using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Utcp.AspNetCore;

internal static class OpenApiRegistration
{
    internal static Type? DocumentServiceType { get; } =
        typeof(Microsoft.AspNetCore.OpenApi.OpenApiOptions).Assembly
            .GetType("Microsoft.AspNetCore.OpenApi.OpenApiDocumentService");

    /// <summary>
    /// Returns true when AddOpenApi(documentName) has registered the keyed document service.
    /// </summary>
    internal static bool IsRegistered(IServiceProvider serviceProvider, string documentName) =>
        DocumentServiceType is not null
        && serviceProvider.GetService<IServiceProviderIsKeyedService>() is { } probe
        && probe.IsKeyedService(DocumentServiceType, documentName);

    internal static InvalidOperationException CreateMissingOpenApiException(string documentName, string calledApi) =>
        new(
            $"{calledApi} requires ASP.NET Core OpenAPI, but the OpenAPI document '{documentName}' is not registered. " +
            "Call builder.Services.AddOpenApi() (from the Microsoft.AspNetCore.OpenApi package) before building the app. " +
            "If you use a custom document name, make sure it matches UtcpOptions.DocumentName " +
            $"(e.g. services.AddOpenApi(\"{documentName}\")).");
}

/// <summary>
/// Fails application startup with a clear error when AddUtcp() was called without AddOpenApi().
/// </summary>
internal sealed class UtcpStartupValidator(IServiceProvider serviceProvider, IOptions<UtcpOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OpenApiRegistration.IsRegistered(serviceProvider, options.Value.DocumentName))
            throw OpenApiRegistration.CreateMissingOpenApiException(options.Value.DocumentName, "AddUtcp()");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
