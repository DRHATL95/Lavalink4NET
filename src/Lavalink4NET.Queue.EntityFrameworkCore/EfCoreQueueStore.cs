namespace Lavalink4NET.Queue.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Queue.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
///     Entity Framework Core-based implementation of <see cref="IQueueStore"/>.
/// </summary>
/// <typeparam name="TContext">The type of the DbContext to use.</typeparam>
public sealed class EfCoreQueueStore<TContext> : IQueueStore
    where TContext : DbContext, IQueueDbContext
{
    private readonly IDbContextFactory<TContext> _contextFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EfCoreQueueStore{TContext}"/> class.
    /// </summary>
    /// <param name="contextFactory">The DbContext factory.</param>
    public EfCoreQueueStore(IDbContextFactory<TContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<QueuedTrackModel>> GetQueueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.QueuedTracks
            .Where(e => e.GuildId == guildId)
            .OrderBy(e => e.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = ToEntity(model);
        context.QueuedTracks.Add(entity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddRangeAsync(
        IEnumerable<QueuedTrackModel> models,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(models);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = models.Select(ToEntity);
        context.QueuedTracks.AddRange(entities);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.QueuedTracks
            .FirstOrDefaultAsync(e => e.GuildId == guildId && e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        context.QueuedTracks.Remove(entity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Decrement positions of all items after the removed entity
        await context.QueuedTracks
            .Where(e => e.GuildId == guildId && e.Position > entity.Position)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Position, e => e.Position - 1), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAtAsync(
        ulong guildId,
        int position,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.QueuedTracks
            .FirstOrDefaultAsync(e => e.GuildId == guildId && e.Position == position, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        context.QueuedTracks.Remove(entity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Decrement positions of all items after the removed position
        await context.QueuedTracks
            .Where(e => e.GuildId == guildId && e.Position > position)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Position, e => e.Position - 1), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveRangeAsync(
        ulong guildId,
        int startPosition,
        int count,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.QueuedTracks
            .Where(e => e.GuildId == guildId && e.Position >= startPosition && e.Position < startPosition + count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.QueuedTracks.RemoveRange(entities);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Update positions of items after the removed range
        if (entities.Count > 0)
        {
            var maxRemovedPosition = entities.Max(e => e.Position);
            await context.QueuedTracks
                .Where(e => e.GuildId == guildId && e.Position > maxRemovedPosition)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Position, e => e.Position - entities.Count), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ClearAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.QueuedTracks
            .Where(e => e.GuildId == guildId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var count = entities.Count;
        context.QueuedTracks.RemoveRange(entities);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return count;
    }

    /// <inheritdoc/>
    public async ValueTask<int> GetCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.QueuedTracks
            .CountAsync(e => e.GuildId == guildId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UpdatePositionsAsync(
        ulong guildId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.QueuedTracks
            .Where(e => e.GuildId == guildId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entityDict = entities.ToDictionary(e => e.Id);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (entityDict.TryGetValue(orderedIds[i], out var entity))
            {
                entity.Position = i;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InsertAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Shift existing items
        var entitiesToShift = await context.QueuedTracks
            .Where(e => e.GuildId == model.GuildId && e.Position >= model.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entity in entitiesToShift)
        {
            entity.Position++;
        }

        // Add new entity
        var newEntity = ToEntity(model);
        context.QueuedTracks.Add(newEntity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<QueuedTrackModel?> DequeueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.QueuedTracks
            .Where(e => e.GuildId == guildId)
            .OrderBy(e => e.Position)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var model = ToModel(entity);
        var dequeuedPosition = entity.Position;
        context.QueuedTracks.Remove(entity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Decrement positions of all items after the dequeued item
        await context.QueuedTracks
            .Where(e => e.GuildId == guildId && e.Position > dequeuedPosition)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Position, e => e.Position - 1), cancellationToken)
            .ConfigureAwait(false);

        return model;
    }

    /// <inheritdoc/>
    public async ValueTask<QueuedTrackModel?> PeekAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.QueuedTracks
            .Where(e => e.GuildId == guildId)
            .OrderBy(e => e.Position)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToModel(entity);
    }

    private static QueuedTrackModel ToModel(QueuedTrackEntity entity)
    {
        return new QueuedTrackModel
        {
            Id = entity.Id,
            GuildId = entity.GuildId,
            Position = entity.Position,
            TrackData = entity.TrackData,
            AddedAt = entity.AddedAt,
            Metadata = entity.Metadata,
        };
    }

    private static QueuedTrackEntity ToEntity(QueuedTrackModel model)
    {
        return new QueuedTrackEntity
        {
            Id = model.Id,
            GuildId = model.GuildId,
            Position = model.Position,
            TrackData = model.TrackData,
            AddedAt = model.AddedAt,
            Metadata = model.Metadata,
        };
    }
}
