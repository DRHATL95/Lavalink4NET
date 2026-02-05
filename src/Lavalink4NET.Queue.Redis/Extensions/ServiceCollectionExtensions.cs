namespace Lavalink4NET.Queue.Redis.Extensions;

using System;
using Lavalink4NET.Queue.Persistence;
using Lavalink4NET.Queue.Persistence.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

/// <summary>
///     Extension methods for registering Redis queue store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Redis-backed persistent queue services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureRedis">Configuration action for Redis options.</param>
    /// <param name="configurePersistence">Optional configuration action for persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisPersistentQueue(
        this IServiceCollection services,
        Action<RedisQueueStoreOptions> configureRedis,
        Action<PersistentQueueOptions>? configurePersistence = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureRedis);

        services.Configure(configureRedis);
        services.TryAddSingleton<IQueueStore, RedisQueueStore>();

        return services.AddPersistentQueue(configurePersistence);
    }

    /// <summary>
    ///     Adds Redis-backed persistent queue services using a connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="configurePersistence">Optional configuration action for persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisPersistentQueue(
        this IServiceCollection services,
        string connectionString,
        Action<PersistentQueueOptions>? configurePersistence = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);

        return services.AddRedisPersistentQueue(
            options => options.ConnectionString = connectionString,
            configurePersistence);
    }

    /// <summary>
    ///     Adds Redis-backed persistent queue services using an existing connection multiplexer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connection">The Redis connection multiplexer.</param>
    /// <param name="configureRedis">Optional configuration action for Redis options.</param>
    /// <param name="configurePersistence">Optional configuration action for persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisPersistentQueue(
        this IServiceCollection services,
        IConnectionMultiplexer connection,
        Action<RedisQueueStoreOptions>? configureRedis = null,
        Action<PersistentQueueOptions>? configurePersistence = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connection);

        services.TryAddSingleton(connection);

        if (configureRedis is not null)
        {
            services.Configure(configureRedis);
        }

        services.TryAddSingleton<IQueueStore>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisQueueStoreOptions>>();
            return new RedisQueueStore(connection, options);
        });

        return services.AddPersistentQueue(configurePersistence);
    }
}
