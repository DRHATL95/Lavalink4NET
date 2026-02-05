namespace Lavalink4NET.Queue.Persistence;

using Lavalink4NET.Players;

/// <summary>
///     Defines the contract for serializing and deserializing track queue items.
/// </summary>
public interface IQueueItemSerializer
{
    /// <summary>
    ///     Serializes a track queue item to a storage model.
    /// </summary>
    /// <param name="item">The queue item to serialize.</param>
    /// <param name="guildId">The guild ID that owns the queue.</param>
    /// <param name="position">The position in the queue.</param>
    /// <returns>The serialized model.</returns>
    QueuedTrackModel Serialize(ITrackQueueItem item, ulong guildId, int position);

    /// <summary>
    ///     Deserializes a storage model to a track queue item.
    /// </summary>
    /// <param name="model">The model to deserialize.</param>
    /// <returns>The deserialized queue item.</returns>
    ITrackQueueItem Deserialize(QueuedTrackModel model);
}
