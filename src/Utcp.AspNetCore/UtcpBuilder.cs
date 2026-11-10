using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Utcp.AspNetCore;

/// <summary>Internal store of post-processing transforms, in registration order.</summary>
internal sealed class UtcpBuilderOptions
{
    public List<Func<UtcpManualContext, CancellationToken, Task>> Transforms { get; } = new();
}

/// <summary>Fluent builder returned by <c>AddUtcp()</c> for registering extra tool sources and manual transforms.</summary>
[PublicAPI]
public sealed class UtcpBuilder
{
    private readonly UtcpBuilderOptions _builderOptions;

    internal UtcpBuilder(IServiceCollection services, UtcpBuilderOptions builderOptions)
    {
        Services = services;
        _builderOptions = builderOptions;
    }

    /// <summary>The underlying service collection, for continued chaining.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Registers a tool source type. Registering the same type twice has no effect.</summary>
    public UtcpBuilder Register<TSource>() where TSource : class, IUtcpToolSource
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IUtcpToolSource, TSource>());
        return this;
    }

    /// <summary>Registers a tool source instance.</summary>
    public UtcpBuilder Register(IUtcpToolSource instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton(instance));
        return this;
    }

    /// <summary>Adds a synchronous post-processing hook, run after all tool sources, in registration order.</summary>
    public UtcpBuilder Configure(Action<UtcpManualContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _builderOptions.Transforms.Add((ctx, _) =>
        {
            configure(ctx);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>Adds an asynchronous post-processing hook, run after all tool sources, in registration order.</summary>
    public UtcpBuilder Configure(Func<UtcpManualContext, CancellationToken, Task> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _builderOptions.Transforms.Add(configure);
        return this;
    }
}
