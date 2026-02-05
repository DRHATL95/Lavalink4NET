namespace Lavalink4NET.Queue.Persistence;

using System;

/// <summary>
///     Represents a serializable model for a queued track that can be stored in external storage.
/// </summary>
public sealed record class QueuedTrackModel
{
    /// <summary>
    ///     Gets or sets the unique identifier for this queue item.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    ///     Gets or sets the guild ID that owns this queue item.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <summary>
    ///     Gets or sets the position in the queue (0-based index).
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    ///     Gets or sets the encoded track data (base64 Lavalink track format).
    /// </summary>
    public required string TrackData { get; init; }

    /// <summary>
    ///     Gets or sets the timestamp when this item was added to the queue.
    /// </summary>
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Gets or sets optional metadata associated with this queue item.
    ///     Can be used for custom data like who requested the track.
    /// </summary>
    public string? Metadata { get; init; }
}
