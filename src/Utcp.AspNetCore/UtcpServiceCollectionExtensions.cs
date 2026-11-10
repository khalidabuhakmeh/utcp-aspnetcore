using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Utcp.AspNetCore;

/// <summary>Dependency-injection registration for UTCP support.</summary>
[PublicAPI]
public static class UtcpServiceCollectionExtensions
{
    /// <summary>
    ///     Registers UTCP services: <see cref="UtcpOptions" />, the singleton manual provider that converts the OpenAPI
    ///     document on demand, and a hosted service that validates configuration at host startup.
    /// </summary>
    /// <remarks>
    ///     Requires ASP.NET Core OpenAPI: call <c>AddOpenApi()</c> as well. If the document named by
    ///     <see cref="UtcpOptions.DocumentName" /> is not registered, host start throws
    ///     <see cref="InvalidOperationException" />
    ///     (rather than failing on the first <c>/utcp</c> request). Calling this method repeatedly does not duplicate the
    ///     validator.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional callback to customize <see cref="UtcpOptions" />.</param>
    /// <returns>A <see cref="UtcpBuilder" /> for registering tool sources and manual hooks.</returns>
    /// <example>
    ///     <code>
    /// builder.Services.AddOpenApi();
    /// builder.Services.AddUtcp(o => o.ManualVersion = "2.1.0");
    ///
    /// var app = builder.Build();
    /// app.MapUtcp();
    /// </code>
    /// </example>
    public static UtcpBuilder AddUtcp(this IServiceCollection services, Action<UtcpOptions>? configure = null)
    {
        var b = services.AddOptions<UtcpOptions>();
        if (configure is not null)
        {
            b.Configure(configure);
        }

        services.AddSingleton<UtcpManualProvider>();
        // Fail fast at host startup (rather than on the first /utcp request) when AddOpenApi() is missing.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, UtcpStartupValidator>());
        var builderOptions = services.FirstOrDefault(d => d.ServiceType == typeof(UtcpBuilderOptions))
            ?.ImplementationInstance as UtcpBuilderOptions;
        if (builderOptions is null)
        {
            builderOptions = new UtcpBuilderOptions();
            services.AddSingleton(builderOptions);
        }

        return new UtcpBuilder(services, builderOptions);
    }
}
