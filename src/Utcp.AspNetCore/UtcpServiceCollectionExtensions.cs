using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Utcp.AspNetCore;

public static class UtcpServiceCollectionExtensions
{
    public static IServiceCollection AddUtcp(this IServiceCollection services, Action<UtcpOptions>? configure = null)
    {
        var b = services.AddOptions<UtcpOptions>();
        if (configure is not null) b.Configure(configure);
        services.AddSingleton<UtcpManualProvider>();
        // Fail fast at host startup (rather than on the first /utcp request) when AddOpenApi() is missing.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, UtcpStartupValidator>());
        return services;
    }
}
