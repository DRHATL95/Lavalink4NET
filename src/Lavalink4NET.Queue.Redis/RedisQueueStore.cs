namespace Lavalink4NET.Queue.Redis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Queue.Persistence;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
///     Redis-based implementation of <see cref="IQueueStore"/>.
/// </summary>
public sealed class RedisQueueStore : IQueueStore, IAsyncDisposable
{
    private readonly IOptions<RedisQueueStoreOptions> _options;
    private readonly IConnectionMultiplexer? _externalConnection;
    private IConnectionMultiplexer? _internalConnection;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisQueueStore"/> class.
    /// </summary>
    /// <param name="options">The Redis queue store options.</param>
    public RedisQueueStore(IOptions<RedisQueueStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisQueueStore"/> class
    ///     with an existing connection multiplexer.
    /// </summary>
    /// <param name="connection">The Redis connection multiplexer.</param>
    /// <param name="options">The Redis queue store options.</param>
    public RedisQueueStore(IConnectionMultiplexer connection, IOptions<RedisQueueStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _externalConnection = connection;
        _options = options;
    }

    private async ValueTask<IDatabase> GetDatabaseAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connection = _externalConnection;

        if (connection is null)
        {
            if (_internalConnection is null)
            {
                var connectionString = _options.Value.ConnectionString
                    ?? throw new InvalidOperationException("Redis connection string is not configured.");

                _internalConnection = await ConnectionMultiplexer.ConnectAsync(connectionString).ConfigureAwait(false);
            }

            connection = _internalConnection;
        }

        return connection.GetDatabase(_options.Value.DatabaseIndex);
    }

    private string GetQueueKey(ulong guildId) => $"{_options.Value.KeyPrefix}{guildId}";
    private string GetItemKey(ulong guildId, Guid id) => $"{_options.Value.KeyPrefix}{guildId}:item:{id}";
    private string GetPositionKey(ulong guildId) => $"{_options.Value.KeyPrefix}{guildId}:positions";

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<QueuedTrackModel>> GetQueueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get ordered item IDs from sorted set
        var itemIds = await db.SortedSetRangeByRankAsync(positionKey).ConfigureAwait(false);

        if (itemIds.Length == 0)
        {
            return Array.Empty<QueuedTrackModel>();
        }

        // Use pipelining to fetch all items in parallel
        var batch = db.CreateBatch();
        var tasks = new List<Task<RedisValue>>(itemIds.Length);
        var validIds = new List<(int Position, Guid Id)>(itemIds.Length);

        for (var i = 0; i < itemIds.Length; i++)
        {
            var idString = (string?)itemIds[i];
            if (idString is not null && Guid.TryParse(idString, out var id))
            {
                validIds.Add((i, id));
                var itemKey = GetItemKey(guildId, id);
                tasks.Add(batch.StringGetAsync(itemKey));
            }
        }

        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = new List<QueuedTrackModel>(validIds.Count);
        for (var i = 0; i < tasks.Count; i++)
        {
            var json = await tasks[i].ConfigureAwait(false);
            if (!json.IsNullOrEmpty)
            {
                var model = JsonSerializer.Deserialize<QueuedTrackModel>((string)json!, JsonOptions);
                if (model is not null)
                {
                    result.Add(model with { Position = validIds[i].Position });
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var itemKey = GetItemKey(model.GuildId, model.Id);
        var positionKey = GetPositionKey(model.GuildId);

        var json = JsonSerializer.Serialize(model, JsonOptions);

        // Store item data
        await db.StringSetAsync(itemKey, json).ConfigureAwait(false);

        // Add to sorted set (position as score)
        var count = await db.SortedSetLengthAsync(positionKey).ConfigureAwait(false);
        await db.SortedSetAddAsync(positionKey, model.Id.ToString(), count).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddRangeAsync(
        IEnumerable<QueuedTrackModel> models,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(models);
        cancellationToken.ThrowIfCancellationRequested();

        var modelsList = models.ToList();
        if (modelsList.Count == 0)
        {
            return;
        }

        var db = await GetDatabaseAsync().ConfigureAwait(false);

        // Group by guild ID to process each guild's queue atomically
        var groupedByGuild = modelsList.GroupBy(m => m.GuildId);

        foreach (var group in groupedByGuild)
        {
            var guildId = group.Key;
            var guildModels = group.ToList();
            var positionKey = GetPositionKey(guildId);

            // Get current queue length
            var currentCount = await db.SortedSetLengthAsync(positionKey).ConfigureAwait(false);

            // Create transaction for this guild
            var transaction = db.CreateTransaction();
            var tasks = new List<Task>();

            for (var i = 0; i < guildModels.Count; i++)
            {
                var model = guildModels[i];
                var itemKey = GetItemKey(guildId, model.Id);
                var json = JsonSerializer.Serialize(model, JsonOptions);

                // Store item data
                tasks.Add(transaction.StringSetAsync(itemKey, json));

                // Add to sorted set with position
                tasks.Add(transaction.SortedSetAddAsync(positionKey, model.Id.ToString(), currentCount + i));
            }

            // Execute transaction
            await transaction.ExecuteAsync().ConfigureAwait(false);
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var itemKey = GetItemKey(guildId, id);
        var positionKey = GetPositionKey(guildId);

        // Remove from sorted set
        var removed = await db.SortedSetRemoveAsync(positionKey, id.ToString()).ConfigureAwait(false);

        // Delete item data
        await db.KeyDeleteAsync(itemKey).ConfigureAwait(false);

        // Reindex remaining items to maintain contiguous 0-based positions
        if (removed)
        {
            var remainingItems = await db.SortedSetRangeByRankAsync(positionKey).ConfigureAwait(false);
            if (remainingItems.Length > 0)
            {
                var entries = remainingItems
                    .Select((item, index) => new SortedSetEntry(item, index))
                    .ToArray();

                await db.SortedSetAddAsync(positionKey, entries).ConfigureAwait(false);
            }
        }

        return removed;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAtAsync(
        ulong guildId,
        int position,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get item at position
        var items = await db.SortedSetRangeByRankAsync(positionKey, position, position).ConfigureAwait(false);

        if (items.Length == 0)
        {
            return false;
        }

        var idString = (string?)items[0];
        if (idString is null || !Guid.TryParse(idString, out var id))
        {
            return false;
        }

        return await RemoveAsync(guildId, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveRangeAsync(
        ulong guildId,
        int startPosition,
        int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get items in range
        var items = await db.SortedSetRangeByRankAsync(positionKey, startPosition, startPosition + count - 1).ConfigureAwait(false);

        var validItems = items
            .Where(item => (string?)item is not null && Guid.TryParse((string?)item, out _))
            .Select(item => Guid.Parse((string)item!))
            .ToList();

        // Parallelize removals
        var tasks = validItems.Select(id => RemoveAsync(guildId, id, cancellationToken).AsTask()).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> ClearAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get all item IDs
        var itemIds = await db.SortedSetRangeByRankAsync(positionKey).ConfigureAwait(false);
        var count = itemIds.Length;

        if (count == 0)
        {
            return 0;
        }

        // Batch delete all item keys
        var itemKeys = itemIds
            .Where(itemId => (string?)itemId is not null && Guid.TryParse((string?)itemId, out _))
            .Select(itemId => (RedisKey)GetItemKey(guildId, Guid.Parse((string)itemId!)))
            .ToArray();

        if (itemKeys.Length > 0)
        {
            await db.KeyDeleteAsync(itemKeys).ConfigureAwait(false);
        }

        // Clear sorted set
        await db.KeyDeleteAsync(positionKey).ConfigureAwait(false);

        return count;
    }

    /// <inheritdoc/>
    public async ValueTask<int> GetCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        return (int)await db.SortedSetLengthAsync(positionKey).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UpdatePositionsAsync(
        ulong guildId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Update scores for each item
        var entries = orderedIds
            .Select((id, index) => new SortedSetEntry(id.ToString(), index))
            .ToArray();

        if (entries.Length > 0)
        {
            await db.SortedSetAddAsync(positionKey, entries).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask InsertAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var itemKey = GetItemKey(model.GuildId, model.Id);
        var positionKey = GetPositionKey(model.GuildId);
        var json = JsonSerializer.Serialize(model, JsonOptions);

        // Use Lua script to make the operation atomic
        var script = @"
            local positionKey = KEYS[1]
            local itemKey = KEYS[2]
            local itemId = ARGV[1]
            local position = tonumber(ARGV[2])
            local itemData = ARGV[3]
            
            -- Get all items with score >= position
            local items = redis.call('ZRANGEBYSCORE', positionKey, position, '+inf', 'WITHSCORES')
            
            -- Increment scores by 1
            for i = 1, #items, 2 do
                local member = items[i]
                local score = tonumber(items[i + 1])
                redis.call('ZADD', positionKey, score + 1, member)
            end
            
            -- Store item data
            redis.call('SET', itemKey, itemData)
            
            -- Add to sorted set at position
            redis.call('ZADD', positionKey, position, itemId)
            
            return 1
        ";

        await db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { positionKey, itemKey },
            new RedisValue[] { model.Id.ToString(), model.Position, json }
        ).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<QueuedTrackModel?> DequeueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get first item
        var items = await db.SortedSetRangeByRankAsync(positionKey, 0, 0).ConfigureAwait(false);

        if (items.Length == 0)
        {
            return null;
        }

        var idString = (string?)items[0];
        if (idString is null || !Guid.TryParse(idString, out var id))
        {
            return null;
        }

        var itemKey = GetItemKey(guildId, id);
        var json = await db.StringGetAsync(itemKey).ConfigureAwait(false);

        if (json.IsNullOrEmpty)
        {
            return null;
        }

        var model = JsonSerializer.Deserialize<QueuedTrackModel>((string)json!, JsonOptions);

        // Remove the item
        await RemoveAsync(guildId, id, cancellationToken).ConfigureAwait(false);

        return model;
    }

    /// <inheritdoc/>
    public async ValueTask<QueuedTrackModel?> PeekAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var positionKey = GetPositionKey(guildId);

        // Get first item
        var items = await db.SortedSetRangeByRankAsync(positionKey, 0, 0).ConfigureAwait(false);

        if (items.Length == 0)
        {
            return null;
        }

        var idString = (string?)items[0];
        if (idString is null || !Guid.TryParse(idString, out var id))
        {
            return null;
        }

        var itemKey = GetItemKey(guildId, id);
        var json = await db.StringGetAsync(itemKey).ConfigureAwait(false);

        if (json.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<QueuedTrackModel>((string)json!, JsonOptions);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_internalConnection is not null)
        {
            await _internalConnection.DisposeAsync().ConfigureAwait(false);
            _internalConnection = null;
        }
    }
}
