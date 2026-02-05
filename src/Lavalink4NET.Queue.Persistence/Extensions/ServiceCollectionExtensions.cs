namespace Lavalink4NET.Queue.Persistence.Extensions;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
///     Extension methods for registering persistent queue services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds persistent queue services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for persistent queue options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistentQueue(
        this IServiceCollection services,
        Action<PersistentQueueOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IQueueItemSerializer, DefaultQueueItemSerializer>();
        services.TryAddSingleton<IPersistentQueueFactory, PersistentQueueFactory>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>
    ///     Adds persistent queue services with a custom queue store implementation.
    /// </summary>
    /// <typeparam name="TStore">The type of the queue store implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for persistent queue options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistentQueue<TStore>(
        this IServiceCollection services,
        Action<PersistentQueueOptions>? configure = null)
        where TStore : class, IQueueStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IQueueStore, TStore>();
        return services.AddPersistentQueue(configure);
    }

    /// <summary>
    ///     Adds persistent queue services with a factory-created queue store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storeFactory">Factory function to create the queue store.</param>
    /// <param name="configure">Optional configuration action for persistent queue options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistentQueue(
        this IServiceCollection services,
        Func<IServiceProvider, IQueueStore> storeFactory,
        Action<PersistentQueueOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(storeFactory);

        services.TryAddSingleton(storeFactory);
        return services.AddPersistentQueue(configure);
    }
}
