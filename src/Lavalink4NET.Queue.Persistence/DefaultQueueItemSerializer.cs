namespace Lavalink4NET.Queue.Persistence;

using System;
using System.Text.Json;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

/// <summary>
///     Default implementation of <see cref="IQueueItemSerializer"/> that serializes tracks
///     using their base64-encoded format.
/// </summary>
public sealed class DefaultQueueItemSerializer : IQueueItemSerializer
{
    /// <summary>
    ///     Gets the singleton instance of the default serializer.
    /// </summary>
    public static DefaultQueueItemSerializer Instance { get; } = new();

    /// <inheritdoc/>
    public QueuedTrackModel Serialize(ITrackQueueItem item, ulong guildId, int position)
    {
        ArgumentNullException.ThrowIfNull(item);

        var trackData = item.Track?.ToString() ?? item.Identifier;

        string? metadata = null;
        if (item is ITrackQueueItemWithMetadata itemWithMetadata && itemWithMetadata.Metadata is not null)
        {
            metadata = JsonSerializer.Serialize(itemWithMetadata.Metadata);
        }

        return new QueuedTrackModel
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            Position = position,
            TrackData = trackData,
            AddedAt = DateTimeOffset.UtcNow,
            Metadata = metadata,
        };
    }

    /// <inheritdoc/>
    public ITrackQueueItem Deserialize(QueuedTrackModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Try to parse as a full track first
        if (LavalinkTrack.TryParse(model.TrackData, provider: null, out var track))
        {
            return new TrackQueueItem(track);
        }

        // Fall back to treating it as an identifier reference
        return new TrackQueueItem(model.TrackData);
    }
}
