namespace Lavalink4NET.Queue.Persistence;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Logging;

/// <summary>
///     A persistent track queue implementation that stores queue state in an external storage backend.
/// </summary>
public sealed class PersistentTrackQueue : ITrackQueue
{
    private readonly IQueueStore _store;
    private readonly IQueueItemSerializer _serializer;
    private readonly ulong _guildId;
    private readonly ILogger<PersistentTrackQueue>? _logger;
    private readonly object _syncRoot = new();

    // Local cache for quick access (synchronized with store)
    private readonly List<ITrackQueueItem> _cachedItems;
    private readonly List<Guid> _cachedIds;
    private static readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private bool _isInitialized;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PersistentTrackQueue"/> class.
    /// </summary>
    /// <param name="store">The queue store to use for persistence.</param>
    /// <param name="guildId">The guild ID that owns this queue.</param>
    /// <param name="serializer">The serializer to use for track items.</param>
    /// <param name="historyCapacity">The capacity for track history (null to disable).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PersistentTrackQueue(
        IQueueStore store,
        ulong guildId,
        IQueueItemSerializer? serializer = null,
        int? historyCapacity = 8,
        ILogger<PersistentTrackQueue>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _guildId = guildId;
        _serializer = serializer ?? DefaultQueueItemSerializer.Instance;
        _logger = logger;
        _cachedItems = new List<ITrackQueueItem>();
        _cachedIds = new List<Guid>();

        History = historyCapacity > 0 ? new TrackHistory(historyCapacity) : null;
    }

    /// <inheritdoc/>
    public ITrackHistory? History { get; }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(History))]
    public bool HasHistory => History is not null;

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            EnsureInitialized();
            lock (_syncRoot)
            {
                return _cachedItems.Count;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEmpty => Count == 0;

    /// <inheritdoc/>
    public ITrackQueueItem this[int index]
    {
        get
        {
            EnsureInitialized();
            lock (_syncRoot)
            {
                return _cachedItems[index];
            }
        }
    }

    /// <summary>
    ///     Initializes the queue by loading data from the store.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            var models = await _store.GetQueueAsync(_guildId, cancellationToken).ConfigureAwait(false);

            lock (_syncRoot)
            {
                _cachedItems.Clear();
                _cachedIds.Clear();

                try
                {
                    foreach (var model in models.OrderBy(m => m.Position))
                    {
                        var item = _serializer.Deserialize(model);
                        _cachedItems.Add(item);
                        _cachedIds.Add(model.Id);
                    }

                    _isInitialized = true;
                }
                finally
                {
                    if (!_isInitialized)
                    {
                        _cachedItems.Clear();
                        _cachedIds.Clear();
                    }
                }
            }

            _logger?.LogDebug("Initialized persistent queue for guild {GuildId} with {Count} items", _guildId, models.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize persistent queue for guild {GuildId}", _guildId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> AddAsync(ITrackQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        int count;
        QueuedTrackModel model;

        lock (_syncRoot)
        {
            var position = _cachedItems.Count;
            model = _serializer.Serialize(item, _guildId, position);

            _cachedItems.Add(item);
            _cachedIds.Add(model.Id);
            count = _cachedItems.Count;
        }

        await _store.AddAsync(model, cancellationToken).ConfigureAwait(false);
        return count;
    }

    /// <inheritdoc/>
    public async ValueTask<int> AddRangeAsync(IReadOnlyList<ITrackQueueItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return Count;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        int count;
        var models = new List<QueuedTrackModel>(items.Count);

        lock (_syncRoot)
        {
            var position = _cachedItems.Count;

            foreach (var item in items)
            {
                var model = _serializer.Serialize(item, _guildId, position++);
                models.Add(model);
                _cachedItems.Add(item);
                _cachedIds.Add(model.Id);
            }

            count = _cachedItems.Count;
        }

        await _store.AddRangeAsync(models, cancellationToken).ConfigureAwait(false);
        return count;
    }

    /// <inheritdoc/>
    public async ValueTask<int> ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        int count;
        lock (_syncRoot)
        {
            count = _cachedItems.Count;
            _cachedItems.Clear();
            _cachedIds.Clear();
        }

        await _store.ClearAsync(_guildId, cancellationToken).ConfigureAwait(false);
        return count;
    }

    /// <inheritdoc/>
    public bool Contains(ITrackQueueItem item)
    {
        EnsureInitialized();
        lock (_syncRoot)
        {
            return _cachedItems.Contains(item);
        }
    }

    /// <inheritdoc/>
    public int IndexOf(ITrackQueueItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureInitialized();

        lock (_syncRoot)
        {
            return _cachedItems.IndexOf(item);
        }
    }

    /// <inheritdoc/>
    public int IndexOf(Func<ITrackQueueItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        EnsureInitialized();

        lock (_syncRoot)
        {
            for (var i = 0; i < _cachedItems.Count; i++)
            {
                if (predicate(_cachedItems[i]))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    public async ValueTask InsertAsync(int index, ITrackQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        QueuedTrackModel model;
        lock (_syncRoot)
        {
            model = _serializer.Serialize(item, _guildId, index);
            _cachedItems.Insert(index, item);
            _cachedIds.Insert(index, model.Id);
        }

        await _store.InsertAsync(model, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InsertRangeAsync(int index, IEnumerable<ITrackQueueItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return;
        }

        IReadOnlyList<Guid> updatedOrder;

        lock (_syncRoot)
        {
            var insertIndex = index;
            foreach (var item in itemsList)
            {
                var model = _serializer.Serialize(item, _guildId, insertIndex);
                _cachedItems.Insert(insertIndex, item);
                _cachedIds.Insert(insertIndex, model.Id);
                insertIndex++;
            }

            updatedOrder = _cachedIds.ToList();
        }

        await _store.UpdatePositionsAsync(_guildId, updatedOrder, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ITrackQueueItem? Peek()
    {
        EnsureInitialized();
        lock (_syncRoot)
        {
            return _cachedItems.Count > 0 ? _cachedItems[0] : null;
        }
    }

    /// <inheritdoc/>
    public bool TryPeek([MaybeNullWhen(false)] out ITrackQueueItem? queueItem)
    {
        queueItem = Peek();
        return queueItem is not null;
    }

    /// <inheritdoc/>
    public async ValueTask<ITrackQueueItem?> TryDequeueAsync(
        TrackDequeueMode dequeueMode = TrackDequeueMode.Normal,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        ITrackQueueItem? item;
        Guid itemId;

        lock (_syncRoot)
        {
            if (_cachedItems.Count == 0)
            {
                return null;
            }

            var index = dequeueMode is TrackDequeueMode.Shuffle
                ? Random.Shared.Next(0, _cachedItems.Count)
                : 0;

            item = _cachedItems[index];
            itemId = _cachedIds[index];
            _cachedItems.RemoveAt(index);
            _cachedIds.RemoveAt(index);
        }

        await _store.RemoveAsync(_guildId, itemId, cancellationToken).ConfigureAwait(false);
        return item;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(ITrackQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        Guid? itemId = null;
        bool removed;

        lock (_syncRoot)
        {
            var index = _cachedItems.IndexOf(item);
            if (index >= 0)
            {
                itemId = _cachedIds[index];
                _cachedItems.RemoveAt(index);
                _cachedIds.RemoveAt(index);
                removed = true;
            }
            else
            {
                removed = false;
            }
        }

        if (itemId.HasValue)
        {
            await _store.RemoveAsync(_guildId, itemId.Value, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAtAsync(int index, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        Guid? itemId = null;
        bool removed;

        lock (_syncRoot)
        {
            if (index < 0 || index >= _cachedItems.Count)
            {
                removed = false;
            }
            else
            {
                itemId = _cachedIds[index];
                _cachedItems.RemoveAt(index);
                _cachedIds.RemoveAt(index);
                removed = true;
            }
        }

        if (itemId.HasValue)
        {
            await _store.RemoveAsync(_guildId, itemId.Value, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    /// <inheritdoc/>
    public async ValueTask<int> RemoveAllAsync(Predicate<ITrackQueueItem> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var idsToRemove = new List<Guid>();

        lock (_syncRoot)
        {
            for (var i = _cachedItems.Count - 1; i >= 0; i--)
            {
                if (predicate(_cachedItems[i]))
                {
                    idsToRemove.Add(_cachedIds[i]);
                    _cachedItems.RemoveAt(i);
                    _cachedIds.RemoveAt(i);
                }
            }
        }

        await Task.WhenAll(idsToRemove.Select(id => _store.RemoveAsync(_guildId, id, cancellationToken).AsTask())).ConfigureAwait(false);

        return idsToRemove.Count;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveRangeAsync(int index, int count, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _cachedItems.RemoveRange(index, count);
            _cachedIds.RemoveRange(index, count);
        }

        await _store.RemoveRangeAsync(_guildId, index, count, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> DistinctAsync(IEqualityComparer<ITrackQueueItem>? equalityComparer = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var comparer = equalityComparer ?? TrackIdentifierEqualityComparer.Instance;
        var idsToRemove = new List<Guid>();
        int difference;

        lock (_syncRoot)
        {
            var previousCount = _cachedItems.Count;
            var seen = new HashSet<ITrackQueueItem>(comparer);
            var indicesToRemove = new List<int>();

            for (var i = 0; i < _cachedItems.Count; i++)
            {
                if (!seen.Add(_cachedItems[i]))
                {
                    indicesToRemove.Add(i);
                    idsToRemove.Add(_cachedIds[i]);
                }
            }

            // Remove in reverse order to maintain valid indices
            for (var i = indicesToRemove.Count - 1; i >= 0; i--)
            {
                var idx = indicesToRemove[i];
                _cachedItems.RemoveAt(idx);
                _cachedIds.RemoveAt(idx);
            }

            difference = previousCount - _cachedItems.Count;
        }

        await Task.WhenAll(idsToRemove.Select(id => _store.RemoveAsync(_guildId, id, cancellationToken).AsTask())).ConfigureAwait(false);

        return difference;
    }

    /// <inheritdoc/>
    public async ValueTask ShuffleAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Guid> newOrder;

        lock (_syncRoot)
        {
            // Fisher-Yates shuffle
            for (var i = 0; i < _cachedItems.Count; i++)
            {
                var targetIndex = i + Random.Shared.Next(_cachedItems.Count - i);

                (_cachedItems[i], _cachedItems[targetIndex]) = (_cachedItems[targetIndex], _cachedItems[i]);
                (_cachedIds[i], _cachedIds[targetIndex]) = (_cachedIds[targetIndex], _cachedIds[i]);
            }

            newOrder = _cachedIds.ToList();
        }

        await _store.UpdatePositionsAsync(_guildId, newOrder, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IEnumerator<ITrackQueueItem> GetEnumerator()
    {
        EnsureInitialized();
        lock (_syncRoot)
        {
            // Return a copy to avoid collection modified during enumeration
            return _cachedItems.ToList().GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            // Use async-over-sync here since we need sync access for IReadOnlyList implementation
            InitializeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _initializationSemaphore.Release();
            }
        }
    }
}

file sealed class TrackIdentifierEqualityComparer : IEqualityComparer<ITrackQueueItem>
{
    public static TrackIdentifierEqualityComparer Instance { get; } = new();

    public bool Equals(ITrackQueueItem? x, ITrackQueueItem? y)
    {
        return GetIdentifier(x) == GetIdentifier(y);
    }

    public int GetHashCode([DisallowNull] ITrackQueueItem obj)
    {
        return GetIdentifier(obj)?.GetHashCode() ?? 0;
    }

    private static string? GetIdentifier(ITrackQueueItem? item)
    {
        return item?.Reference.Identifier ?? item?.Reference.Track?.Identifier;
    }
}
