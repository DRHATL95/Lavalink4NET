namespace Lavalink4NET.Queue.Persistence.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     In-memory implementation of <see cref="IQueueStore"/> for testing purposes.
/// </summary>
public sealed class InMemoryQueueStore : IQueueStore
{
    private readonly ConcurrentDictionary<ulong, List<QueuedTrackModel>> _queues = new();

    public ValueTask<IReadOnlyList<QueuedTrackModel>> GetQueueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            return ValueTask.FromResult<IReadOnlyList<QueuedTrackModel>>(
                queue.OrderBy(x => x.Position).ToList());
        }
    }

    public ValueTask AddAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(model.GuildId);
        lock (queue)
        {
            queue.Add(model);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AddRangeAsync(
        IEnumerable<QueuedTrackModel> models,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(models);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var model in models)
        {
            var queue = GetOrCreateQueue(model.GuildId);
            lock (queue)
            {
                queue.Add(model);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> RemoveAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var item = queue.FirstOrDefault(x => x.Id == id);
            if (item is null)
            {
                return ValueTask.FromResult(false);
            }

            queue.Remove(item);

            // Update positions of remaining items
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].Position > item.Position)
                {
                    queue[i] = queue[i] with { Position = queue[i].Position - 1 };
                }
            }

            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> RemoveAtAsync(
        ulong guildId,
        int position,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var item = queue.FirstOrDefault(x => x.Position == position);
            if (item is null)
            {
                return ValueTask.FromResult(false);
            }

            queue.Remove(item);

            // Update positions of remaining items
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].Position > position)
                {
                    queue[i] = queue[i] with { Position = queue[i].Position - 1 };
                }
            }

            return ValueTask.FromResult(true);
        }
    }

    public ValueTask RemoveRangeAsync(
        ulong guildId,
        int startPosition,
        int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var itemsToRemove = queue
                .Where(x => x.Position >= startPosition && x.Position < startPosition + count)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                queue.Remove(item);
            }

            // Update positions of items after the removed range
            var maxRemovedPosition = startPosition + count - 1;
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].Position > maxRemovedPosition)
                {
                    queue[i] = queue[i] with { Position = queue[i].Position - count };
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<int> ClearAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var count = queue.Count;
            queue.Clear();
            return ValueTask.FromResult(count);
        }
    }

    public ValueTask<int> GetCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            return ValueTask.FromResult(queue.Count);
        }
    }

    public ValueTask UpdatePositionsAsync(
        ulong guildId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            for (var i = 0; i < orderedIds.Count; i++)
            {
                var item = queue.FirstOrDefault(x => x.Id == orderedIds[i]);
                if (item is not null)
                {
                    var index = queue.IndexOf(item);
                    queue[index] = item with { Position = i };
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask InsertAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(model.GuildId);
        lock (queue)
        {
            // Shift existing items
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].Position >= model.Position)
                {
                    queue[i] = queue[i] with { Position = queue[i].Position + 1 };
                }
            }

            queue.Add(model);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<QueuedTrackModel?> DequeueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var item = queue.OrderBy(x => x.Position).FirstOrDefault();
            if (item is null)
            {
                return ValueTask.FromResult<QueuedTrackModel?>(null);
            }

            queue.Remove(item);

            // Update positions of remaining items
            for (var i = 0; i < queue.Count; i++)
            {
                queue[i] = queue[i] with { Position = queue[i].Position - 1 };
            }

            return ValueTask.FromResult<QueuedTrackModel?>(item);
        }
    }

    public ValueTask<QueuedTrackModel?> PeekAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = GetOrCreateQueue(guildId);
        lock (queue)
        {
            var item = queue.OrderBy(x => x.Position).FirstOrDefault();
            return ValueTask.FromResult<QueuedTrackModel?>(item);
        }
    }

    private List<QueuedTrackModel> GetOrCreateQueue(ulong guildId)
    {
        return _queues.GetOrAdd(guildId, _ => new List<QueuedTrackModel>());
    }
}
