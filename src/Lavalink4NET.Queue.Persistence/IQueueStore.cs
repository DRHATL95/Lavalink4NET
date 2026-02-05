namespace Lavalink4NET.Queue.Persistence;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Defines the contract for a persistent queue storage backend.
/// </summary>
public interface IQueueStore
{
    /// <summary>
    ///     Retrieves all queued tracks for a specific guild, ordered by position.
    /// </summary>
    /// <param name="guildId">The guild ID to retrieve tracks for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of queued track models ordered by position.</returns>
    ValueTask<IReadOnlyList<QueuedTrackModel>> GetQueueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a track to the queue at the specified position.
    /// </summary>
    /// <param name="model">The track model to add.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask AddAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds multiple tracks to the queue.
    /// </summary>
    /// <param name="models">The track models to add.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask AddRangeAsync(
        IEnumerable<QueuedTrackModel> models,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a track from the queue by its ID.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="id">The unique ID of the queue item to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
    ValueTask<bool> RemoveAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a track from the queue at the specified position.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="position">The position of the item to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
    ValueTask<bool> RemoveAtAsync(
        ulong guildId,
        int position,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a range of tracks from the queue starting at the specified position.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="startPosition">The starting position (inclusive).</param>
    /// <param name="count">The number of items to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask RemoveRangeAsync(
        ulong guildId,
        int startPosition,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all tracks from a guild's queue.
    /// </summary>
    /// <param name="guildId">The guild ID to clear the queue for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The number of items that were removed.</returns>
    ValueTask<int> ClearAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the number of tracks in a guild's queue.
    /// </summary>
    /// <param name="guildId">The guild ID to get the count for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The number of tracks in the queue.</returns>
    ValueTask<int> GetCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the positions of all tracks in the queue.
    ///     Used after shuffle or reorder operations.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="orderedIds">The track IDs in their new order.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask UpdatePositionsAsync(
        ulong guildId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Inserts a track at the specified position, shifting existing items.
    /// </summary>
    /// <param name="model">The track model to insert.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask InsertAsync(
        QueuedTrackModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dequeues and removes the first track from the queue.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The dequeued track model, or <c>null</c> if the queue is empty.</returns>
    ValueTask<QueuedTrackModel?> DequeueAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Peeks at the first track in the queue without removing it.
    /// </summary>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The first track model, or <c>null</c> if the queue is empty.</returns>
    ValueTask<QueuedTrackModel?> PeekAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);
}
