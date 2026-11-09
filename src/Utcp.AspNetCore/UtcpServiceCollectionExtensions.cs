using Microsoft.Extensions.DependencyInjection;

namespace Utcp.AspNetCore;

public static class UtcpServiceCollectionExtensions
{
    public static IServiceCollection AddUtcp(this IServiceCollection services, Action<UtcpOptions>? configure = null)
    {
        var b = services.AddOptions<UtcpOptions>();
        if (configure is not null) b.Configure(configure);
        services.AddSingleton<UtcpManualProvider>();
        return services;
    }
}
