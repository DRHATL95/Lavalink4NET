namespace Lavalink4NET.Queue.Persistence;

using Lavalink4NET.Players;

/// <summary>
///     Represents a track queue item that carries additional metadata for persistence.
/// </summary>
public interface ITrackQueueItemWithMetadata : ITrackQueueItem
{
    /// <summary>
    ///     Gets the metadata associated with this queue item.
    /// </summary>
    object? Metadata { get; }
}
