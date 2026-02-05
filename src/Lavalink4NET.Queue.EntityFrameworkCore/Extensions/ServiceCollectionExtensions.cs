namespace Lavalink4NET.Queue.EntityFrameworkCore.Extensions;

using System;
using Lavalink4NET.Queue.Persistence;
using Lavalink4NET.Queue.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
///     Extension methods for registering Entity Framework Core queue store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Entity Framework Core-backed persistent queue services to the service collection.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that implements <see cref="IQueueDbContext"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configurePersistence">Optional configuration action for persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCorePersistentQueue<TContext>(
        this IServiceCollection services,
        Action<PersistentQueueOptions>? configurePersistence = null)
        where TContext : DbContext, IQueueDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IQueueStore, EfCoreQueueStore<TContext>>();

        return services.AddPersistentQueue(configurePersistence);
    }
}
